import json
import pandas as pd
from sqlalchemy import create_engine
from datetime import datetime, timedelta
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import mean_absolute_error

def get_forecast_data():
    engine = create_engine('postgresql://postgres:5432@127.0.0.1:5432/FlowerShopPPO')

    # 1. Прогноз количества заказов
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

    features = orders_model.feature_names_in_
    future_dates['predicted_orders'] = orders_model.predict(future_dates[features])
    #print(f"MAE: {mean_absolute_error(y_test, predictions)}")
    #print(f"Среднее значение: {y_test.mean()}")

    # 2. Прогноз спроса на товары (на основе последних 6 недель)
    sales_query = """
    SELECT 
        n.id AS nomenclature_id,
        n.name AS nomenclature_name,
        o.reg_date,
        EXTRACT(DOW FROM o.reg_date) AS day_of_week,
        SUM(op.amount) AS total_sold
    FROM order_product_in_stock op
    JOIN "order" o ON op.id_order = o.id
    JOIN product_in_stock pis ON op.id_product = pis.id
    JOIN nomenclature n ON pis.id_nomenclature = n.id
    WHERE o.reg_date >= CURRENT_DATE - INTERVAL '6 weeks'
    GROUP BY n.id, n.name, o.reg_date, EXTRACT(DOW FROM o.reg_date)
    """
    sales_df = pd.read_sql(sales_query, engine)
    sales_df['reg_date'] = pd.to_datetime(sales_df['reg_date'])

    # 3. Получаем текущие остатки
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

    # 4. Вычисляем средние продажи по дням недели
    avg_daily_sales = sales_df.groupby([
        'nomenclature_id',
        'nomenclature_name',
        'day_of_week'
    ])['total_sold'].mean().reset_index()

    # 5. Создаем прогноз на следующую неделю
    forecast_data = []
    products = avg_daily_sales[['nomenclature_id', 'nomenclature_name']].drop_duplicates()

    for _, day in future_dates.iterrows():
        for _, product in products.iterrows():
            forecast_data.append({
                'date': day['date'],
                'day_of_week': day['day_of_week'],
                'nomenclature_id': product['nomenclature_id'],
                'nomenclature_name': product['nomenclature_name']
            })

    forecast_df = pd.DataFrame(forecast_data)
    forecast_df = pd.merge(
        forecast_df,
        avg_daily_sales,
        on=['nomenclature_id', 'nomenclature_name', 'day_of_week'],
        how='left'
    )
    forecast_df['total_sold'] = forecast_df['total_sold'].fillna(0)

    # 6. Рассчитываем рекомендации по закупкам
    product_recommendations = forecast_df.groupby([
        'nomenclature_id',
        'nomenclature_name'
    ])['total_sold'].sum().reset_index()
    product_recommendations.rename(columns={'total_sold': 'predicted_demand'}, inplace=True)

    product_recommendations = pd.merge(
        product_recommendations,
        stock_df,
        on=['nomenclature_id', 'nomenclature_name'],
        how='left'
    )
    product_recommendations['current_stock'] = product_recommendations['current_stock'].fillna(0)
    product_recommendations['recommended_order'] = (
        product_recommendations['predicted_demand'] // 6 - product_recommendations['current_stock']
    ).apply(lambda x: max(0, round(x)))

    # 7. Формируем итоговый результат
    daily_orders = []
    for _, day in future_dates.iterrows():
        daily_orders.append({
            'date': day['date'].strftime('%Y-%m-%d'),
            'day_of_week': int(day['day_of_week']),
            'orders': round(float(day['predicted_orders']))
        })

    products_list = []
    for _, product in product_recommendations[product_recommendations['recommended_order'] > 0].iterrows():
        if (product['predicted_demand'] // 6 > 0):
            products_list.append({
                'product_id': int(product['nomenclature_id']),
                'product_name': product['nomenclature_name'],
                'predicted_demand': round(float(product['predicted_demand'])),
                'current_stock': int(product['current_stock']),
                'amount': int(product['recommended_order'])
            })

    result = {
        'total_orders': int(future_dates['predicted_orders'].sum()),
        'forecast_start_date': future_dates['date'].min().strftime('%Y-%m-%d'),
        'forecast_end_date': future_dates['date'].max().strftime('%Y-%m-%d'),
        'daily_forecast': daily_orders,
        'products': products_list
    }

    return json.dumps(result, ensure_ascii=False, indent=2)

if __name__ == "__main__":
    data = get_forecast_data()
    with open('forecast_output.json', 'w', encoding='utf-8') as f:
        f.write(data)
    print(data)