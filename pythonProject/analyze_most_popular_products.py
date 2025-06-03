import pandas as pd
import matplotlib.pyplot as plt
from sqlalchemy import create_engine
from sklearn.ensemble import RandomForestRegressor
from sklearn.preprocessing import LabelEncoder
from datetime import datetime, timedelta
import joblib
import openpyxl

def connect_db():
    engine = create_engine('postgresql://postgres:5432@127.0.0.1:5432/FlowerShopPPO')
    return engine

def load_data(engine):
    sales_query = """
    SELECT 
        n.id AS nomenclature_id,
        n.name AS nomenclature_name,
        EXTRACT(DOW FROM o.reg_date) AS day_of_week,
        EXTRACT(MONTH FROM o.reg_date) AS month,
        SUM(op.amount) AS total_sold
    FROM order_product_in_stock op
    JOIN "order" o ON op.id_order = o.id
    JOIN product_in_stock pis ON op.id_product = pis.id
    JOIN nomenclature n ON pis.id_nomenclature = n.id
    GROUP BY n.id, n.name, EXTRACT(DOW FROM o.reg_date), EXTRACT(MONTH FROM o.reg_date)
    """
    sales_df = pd.read_sql(sales_query, engine)

    stock_query = """
    SELECT 
        n.id AS nomenclature_id,
        n.name AS nomenclature_name,
        SUM(pis.amount) AS current_stock
    FROM product_in_stock pis
    JOIN nomenclature n ON pis.id_nomenclature = n.id
    GROUP BY n.id, n.name
    """
    stock_df = pd.read_sql(stock_query, engine)

    return sales_df, stock_df


def prepare_and_train(sales_df):
    le = LabelEncoder()
    sales_df['nomenclature_id_encoded'] = le.fit_transform(sales_df['nomenclature_id'])
    joblib.dump(le, 'nomenclature_encoder.pkl')

    X = sales_df[['nomenclature_id_encoded', 'day_of_week', 'month']]
    y = sales_df['total_sold']

    model = RandomForestRegressor(n_estimators=100, random_state=42)
    model.fit(X, y)

    return model, le



def generate_recommendations(model, le, stock_df, days=7):
    dates = [datetime.now() + timedelta(days=i) for i in range(1, days + 1)]

    products = stock_df[['nomenclature_id', 'nomenclature_name']].drop_duplicates()

    forecast_data = []
    for _, product in products.iterrows():
        for date in dates:
            forecast_data.append({
                'nomenclature_id': product['nomenclature_id'],
                'nomenclature_name': product['nomenclature_name'],
                'day_of_week': date.weekday(),
                'month': date.month,
                'date': date.date()
            })

    forecast_df = pd.DataFrame(forecast_data)
    forecast_df['nomenclature_id_encoded'] = le.transform(forecast_df['nomenclature_id'])

    X_forecast = forecast_df[['nomenclature_id_encoded', 'day_of_week', 'month']]
    forecast_df['predicted_demand'] = model.predict(X_forecast)

    demand_forecast = forecast_df.groupby(['nomenclature_id', 'nomenclature_name'])['predicted_demand'].sum().reset_index()

    recommendations = pd.merge(demand_forecast, stock_df, on=['nomenclature_id', 'nomenclature_name'], how='left')
    recommendations['current_stock'] = recommendations['current_stock'].fillna(0)

    recommendations['recommended_order'] = (
                recommendations['predicted_demand'] - recommendations['current_stock']).apply(
        lambda x: max(0, round(x)))

    recommendations = recommendations.sort_values('recommended_order', ascending=False)

    return recommendations


def visualize_recommendations(recommendations):
    plt.figure(figsize=(14, 8))

    top_products = recommendations[recommendations['recommended_order'] > 0].head(10)

    if len(top_products) > 0:
        plt.barh(top_products['product_name'], top_products['recommended_order'], color='skyblue')
        plt.xlabel('Рекомендуемое количество для заказа')
        plt.title('Топ-10 товаров для заказа на неделю')
        plt.gca().invert_yaxis()
        plt.grid(axis='x', linestyle='--', alpha=0.7)

        for index, value in enumerate(top_products['recommended_order']):
            plt.text(value + 0.5, index, str(int(value)), va='center')

        plt.tight_layout()
        plt.savefig('order_recommendations.png', dpi=300)
        plt.show()
    else:
        print("Все товары в достаточном количестве, заказ не требуется")


def product_recomendation():
    engine = connect_db()
    sales_df, stock_df = load_data(engine)

    model, le = prepare_and_train(sales_df)
    recommendations = generate_recommendations(model, le, stock_df)

    print("\nРекомендации по закупкам на неделю:")
    print(recommendations[['nomenclature_name', 'current_stock', 'predicted_demand', 'recommended_order']])
    return recommendations[['nomenclature_name', 'current_stock', 'predicted_demand', 'recommended_order']]

#visualize_recommendations(recommendations)

#recommendations.to_excel('purchase_recommendations.xlsx', index=False)
#print("\nРекомендации сохранены в файл purchase_recommendations.xlsx")
