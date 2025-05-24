import pandas as pd
import matplotlib.pyplot as plt
import seaborn as sns
from sqlalchemy import create_engine
from sklearn.ensemble import RandomForestRegressor
from sklearn.model_selection import train_test_split
from sklearn.metrics import mean_absolute_error
from datetime import datetime, timedelta

plt.style.use('classic')
sns.set_theme(style="ticks", palette="deep")

def connect_db():
    engine = create_engine('postgresql://postgres:5432@127.0.0.1:5432/FlowerShopPPO')
    return engine

def load_and_prepare_data():
    engine = connect_db()

    query = """
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
    df = pd.read_sql(query, engine)

    df['reg_date'] = pd.to_datetime(df['reg_date'])
    df['year'] = df['reg_date'].dt.year
    df['day_of_year'] = df['reg_date'].dt.dayofyear
    df['week_of_year'] = df['reg_date'].dt.isocalendar().week

    return df


def train_model(df):
    X = df.drop(['reg_date', 'order_count'], axis=1)
    y = df['order_count']

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

    model = RandomForestRegressor(n_estimators=100, random_state=42)
    model.fit(X_train, y_train)

    predictions = model.predict(X_test)
    mae = mean_absolute_error(y_test, predictions)
    print(f"Средняя абсолютная ошибка (MAE): {mae:.2f}")

    return model


def create_forecast_and_plot(model):
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

    features = model.feature_names_in_
    future_dates['predicted_orders'] = model.predict(future_dates[features])

    '''plt.figure(figsize=(14, 7))
    ax = plt.gca()

    date_labels = future_dates['date'].dt.strftime('%a\n%d %b')

    bars = plt.bar(
        date_labels,
        future_dates['predicted_orders'],
        color='#3498db',
        alpha=0.7,
        label='Прогноз'
    )

    for bar in bars:
        height = bar.get_height()
        ax.text(
            bar.get_x() + bar.get_width() / 2.,
            height + 0.3,
            f'{height:.1f}',
            ha='center',
            va='bottom',
            fontsize=10,
            bbox=dict(facecolor='white', alpha=0.8, edgecolor='none', pad=0.3)
        )

    plt.title('Прогноз количества заказов на неделю', fontsize=16, pad=20)
    plt.xlabel('Дата', fontsize=12, labelpad=10)
    plt.ylabel('Количество заказов', fontsize=12, labelpad=10)
    plt.grid(True, linestyle='--', alpha=0.6, axis='y')
    plt.ylim(0, future_dates['predicted_orders'].max() * 1.2)  # Добавляем немного места сверху
    plt.legend(frameon=True, loc='upper right')

    plt.xticks(rotation=0)

    plt.tight_layout()
    plt.savefig('weekly_orders_forecast_bar.png', dpi=300, bbox_inches='tight')
    plt.show()'''

    return future_dates

def forecast_of_orders():
    df = load_and_prepare_data()
    # пока что обучение будет предполагаться каждый раз при выводе данной аналитики
    # тк данные предполагаются динамическими
    model = train_model(df)
    forecast = create_forecast_and_plot(model)

    print("\nДетальный прогноз на неделю:")
    forecast['date'] = forecast['date'].dt.strftime('%Y-%m-%d (%A)')
    print(forecast[['date', 'predicted_orders']].to_string(index=False))

forecast_of_orders()