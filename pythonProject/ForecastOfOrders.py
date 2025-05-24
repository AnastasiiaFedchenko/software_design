import json
import pandas as pd
from sqlalchemy import create_engine
from sklearn.ensemble import RandomForestRegressor
from sklearn.preprocessing import LabelEncoder
from datetime import datetime, timedelta


def get_forecast_data():
    engine = create_engine('postgresql://postgres:5432@127.0.0.1:5432/FlowerShopPPO')

    orders_query = """
    select 
        reg_date,
        extract(dow from reg_date) as day_of_week,
        extract(month from reg_date) as month,
        extract(day from reg_date) as day_of_month,
        case when c.type = 'поставщик' then 1 else 0 end as is_supplier,
        count(*) as order_count
    from "order" o
    join counterpart c on o.counterpart = c.id
    group by reg_date, c.type
    """
    orders_df = pd.read_sql(orders_query, engine)
    orders_df['reg_date'] = pd.to_datetime(orders_df['reg_date'])
    orders_df['year'] = orders_df['reg_date'].dt.year
    orders_df['day_of_year'] = orders_df['reg_date'].dt.dayofyear
    orders_df['week_of_year'] = orders_df['reg_date'].dt.isocalendar().week

    X_orders = orders_df.drop(['reg_date', 'order_count'], axis=1)
    y_orders = orders_df['order_count']
    orders_model = RandomForestRegressor(n_estimators=100, random_state=42)
    orders_model.fit(X_orders, y_orders)

    future_dates = pd.DataFrame({
        'date': [datetime.now() + timedelta(days=i) for i in range(7)]
    })
    future_dates['day_of_week'] = future_dates['date'].dt.dayofweek
    future_dates['month'] = future_dates['date'].dt.month
    future_dates['day_of_month'] = future_dates['date'].dt.day
    future_dates['day_of_year'] = future_dates['date'].dt.dayofyear
    future_dates['week_of_year'] = future_dates['date'].dt.isocalendar().week
    future_dates['year'] = future_dates['date'].dt.year
    future_dates['is_supplier'] = 0

    # Получаем прогноз
    features = orders_model.feature_names_in_
    future_dates['predicted_orders'] = orders_model.predict(future_dates[features])

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

    le = LabelEncoder()
    sales_df['nomenclature_id_encoded'] = le.fit_transform(sales_df['nomenclature_id'])

    X_products = sales_df[['nomenclature_id_encoded', 'day_of_week', 'month']]
    y_products = sales_df['total_sold']
    products_model = RandomForestRegressor(n_estimators=100, random_state=42)
    products_model.fit(X_products, y_products)

    products = stock_df[['nomenclature_id', 'nomenclature_name']].drop_duplicates()
    forecast_data = []

    for _, product in products.iterrows():
        for _, day in future_dates.iterrows():
            forecast_data.append({
                'nomenclature_id': product['nomenclature_id'],
                'nomenclature_name': product['nomenclature_name'],
                'day_of_week': day['day_of_week'],
                'month': day['month'],
                'date': day['date'].strftime('%Y-%m-%d')
            })

    forecast_df = pd.DataFrame(forecast_data)
    forecast_df['nomenclature_id_encoded'] = le.transform(forecast_df['nomenclature_id'])
    X_forecast = forecast_df[['nomenclature_id_encoded', 'day_of_week', 'month']]
    forecast_df['predicted_demand'] = products_model.predict(X_forecast)

    product_recommendations = forecast_df.groupby(['nomenclature_id', 'nomenclature_name'])[
        'predicted_demand'].sum().reset_index()
    product_recommendations = pd.merge(product_recommendations, stock_df, on=['nomenclature_id', 'nomenclature_name'],
                                       how='left')
    product_recommendations['current_stock'] = product_recommendations['current_stock'].fillna(0)
    product_recommendations['recommended_order'] = (
                product_recommendations['predicted_demand'] - product_recommendations['current_stock']).apply(
        lambda x: max(0, round(x)))

    # Подготовка данных для C#
    daily_orders = []
    for _, day in future_dates.iterrows():
        daily_orders.append({
            'date': day['date'].strftime('%Y-%m-%d'),
            'day_of_week': int(day['day_of_week']),
            'orders': round(float(day['predicted_orders']))
        })

    products_list = []
    for _, product in product_recommendations[product_recommendations['recommended_order'] > 0].iterrows():
        products_list.append({
            'product_id': int(product['nomenclature_id']),
            'product_name': product['nomenclature_name'],
            'amount': int(product['recommended_order'])
        })

    result = {
        'total_orders': int(future_dates['predicted_orders'].sum()),
        'total_products': int(product_recommendations['recommended_order'].sum()),
        'daily_forecast': daily_orders,
        'products': products_list
    }

    return json.dumps(result, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    data = get_forecast_data()
    with open('forecast_output.json', 'w', encoding='utf-8') as f:
        f.write(data)
    print(data)