from sqlalchemy import create_engine
import pandas as pd


def connect_db():
    engine = create_engine('postgresql://postgres:5432@127.0.0.1:5432/FlowerShopPPO')
    return engine


def get_top_sellers_by_avg_check(engine):
    """Получение топ-10 продавцов с наибольшим средним чеком"""
    try:
        query = """
        SELECT 
            u.id AS seller_id,
            u.name AS seller_name,
            AVG(op.amount * p.selling_price) AS avg_check,
            COUNT(o.id) AS orders_count,
            SUM(op.amount * p.selling_price) AS total_sales
        FROM 
            "user" u
        JOIN 
            "order" o ON u.id = o.responsible
        JOIN 
            order_product_in_stock op ON o.id = op.id_order
        JOIN
            price p ON op.price = p.id
        WHERE 
            u.type = 'продавец'
        GROUP BY 
            u.id, u.name
        HAVING
            COUNT(o.id) > 5 
        ORDER BY 
            avg_check DESC
        LIMIT 10;
        """

        with engine.connect() as connection:
            df_sellers = pd.read_sql(query, connection)

        return df_sellers

    except Exception as e:
        print(f"Ошибка при получении данных о продавцах: {e}")
        return None


def get_overall_avg_check(engine):
    """Получение средней цены чека по всем заказам"""
    try:
        query = """
        SELECT 
            AVG(op.amount * p.selling_price) AS overall_avg_check
        FROM 
            order_product_in_stock op
        JOIN
            price p ON op.price = p.id;
        """

        with engine.connect() as connection:
            result = connection.execute(query)
            overall_avg = result.fetchone()[0]

        return overall_avg

    except Exception as e:
        print(f"Ошибка при расчете средней цены чека: {e}")
        return None


def analyze_sellers_performance():
    engine = connect_db()
    if engine:
        # Получаем топ продавцов по среднему чеку
        top_sellers = get_top_sellers_by_avg_check(engine)

        # Получаем общую среднюю цену чека
        overall_avg = get_overall_avg_check(engine)

        return top_sellers, overall_avg
    else:
        return None, None


# Получаем результаты
top_sellers, overall_avg_check = analyze_sellers_performance()

# Выводим результаты
print("Топ-10 продавцов с наибольшим средним чеком:")
print(top_sellers[['seller_id', 'seller_name', 'avg_check', 'orders_count']])
print(f"\nСредняя цена чека по всем заказам: {overall_avg_check}")