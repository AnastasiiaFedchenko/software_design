create table country(
	id int primary key,
	name text
);

create table nomenclature(
	id int primary key,
	name text,
	country_id int references country(id)
);

create type counterpart_role as enum ('поставщик', 'покупатель');

create type legal_status_type as enum (
    'Физическое лицо',
    'Юридическое лицо',
    'Индивидуальный предприниматель'
);

create table counterpart(
	id int primary key,
	name text,
	type counterpart_role,
	legal_status legal_status_type,
	legal_address jsonb,
	contact_person text,
	phone varchar(20)
);

create type user_role as enum ('администратор', 'продавец', 'кладовщик');

create table "user"(
	id int primary key,
	name text,
	type user_role
);
ALTER TABLE "user" ADD COLUMN password VARCHAR(255);

CREATE TABLE batch_of_products(
    id_product_batch int,
    id_nomenclature int REFERENCES nomenclature(id),
    production_date date,
    expiration_date date,
    cost_price decimal(12,2),  -- Себестоимость
    amount int,
    responsible int REFERENCES "user"(id),
    suppliers int REFERENCES counterpart(id),
    
    PRIMARY KEY (id_product_batch, id_nomenclature)
);

CREATE TABLE write_off(
    id int PRIMARY KEY,
    id_product_batch int,
    id_nomenclature int,
    amount int,
    responsible int REFERENCES "user"(id),
    FOREIGN KEY (id_product_batch, id_nomenclature) 
        REFERENCES batch_of_products(id_product_batch, id_nomenclature)
);

CREATE TABLE price(
    id int PRIMARY KEY,
    id_nomenclature int REFERENCES nomenclature(id),
    selling_price decimal(12,2),  -- Цена продажи
    id_product_batch int,
    FOREIGN KEY (id_product_batch, id_nomenclature) 
        REFERENCES batch_of_products(id_product_batch, id_nomenclature)
);

create table storage_place(
	id int primary key,
	name text,
	address JSONB
);


CREATE TABLE product_in_stock(
    id int PRIMARY KEY,
    id_nomenclature int,
    id_product_batch int,
    amount int,
    storage_place int REFERENCES storage_place(id),
    FOREIGN KEY (id_product_batch, id_nomenclature) 
        REFERENCES batch_of_products(id_product_batch, id_nomenclature)
);

ALTER TABLE public.product_in_stock ADD CONSTRAINT uq_product_in_stock_nomenclature_batch UNIQUE (id_nomenclature, id_product_batch);

create table "order"(
	id int primary key,
	reg_date date,
	counterpart int references counterpart(id),
	responsible int references "user"(id)
);
create table "order_product_in_stock"(
	id_order int references "order"(id),
	id_product int references product_in_stock(id),
	amount int,
	price int references price(id)
);

CREATE TYPE order_status_type AS ENUM ('Новый', 'Подтверждён', 'Собран', 'Получен');

create table sales(
	receipt_number int primary key,
	counterpart int references counterpart(id),
	order_id int references "order"(id),
	order_status order_status_type,
	final_price decimal(12,2)
);

--drop TABLE sales;

-- Для таблицы country
ALTER TABLE country ADD CONSTRAINT check_country_name_length CHECK (length(name) > 0);

-- Для таблицы nomenclature
ALTER TABLE nomenclature ADD CONSTRAINT check_nomenclature_name_length CHECK (length(name) > 0);
ALTER TABLE nomenclature ADD CONSTRAINT check_country_reference CHECK (country_id IS NOT NULL);

-- Для таблицы counterpart
ALTER TABLE counterpart ADD CONSTRAINT check_counterpart_name_length CHECK (length(name) > 0);
ALTER TABLE counterpart ADD CONSTRAINT check_contact_person_length CHECK (length(contact_person) > 0);
ALTER TABLE counterpart ADD CONSTRAINT check_phone_format CHECK (phone ~ '^\+?[0-9]{10,15}$');

-- Для таблицы user
ALTER TABLE "user" ADD CONSTRAINT check_user_name_length CHECK (length(name) > 1);

-- Для таблицы batch_of_products
ALTER TABLE batch_of_products ADD CONSTRAINT check_production_date CHECK (production_date <= current_date);
ALTER TABLE batch_of_products ADD CONSTRAINT check_expiration_date CHECK (expiration_date > production_date);
ALTER TABLE batch_of_products ADD CONSTRAINT check_positive_cost_price CHECK (cost_price > 0);
ALTER TABLE batch_of_products ADD CONSTRAINT check_positive_amount CHECK (amount > 0);

-- Для таблицы write_off
ALTER TABLE write_off ADD CONSTRAINT check_positive_write_off_amount CHECK (amount >= 0);

-- Для таблицы price
ALTER TABLE price ADD CONSTRAINT check_positive_selling_price CHECK (selling_price > 0);

-- Для таблицы storage_place
ALTER TABLE storage_place ADD CONSTRAINT check_storage_name_length CHECK (length(name) > 0);

-- Для таблицы product_in_stock
ALTER TABLE product_in_stock ADD CONSTRAINT check_positive_stock_amount CHECK (amount >= 0);

-- Для таблицы order
ALTER TABLE "order_product_in_stock"
	 add constraint fk_product_id foreign key (id_product) references product_in_stock;
	 
ALTER TABLE "order_product_in_stock"
	 add constraint fk_order_id foreign key (id_order) references "order";
-- Для таблицы sales
ALTER TABLE sales ADD CONSTRAINT check_positive_final_price CHECK (final_price > 0);

CREATE SEQUENCE order_id_seq OWNED BY "order".id;
SELECT setval('order_id_seq', (SELECT MAX(id) FROM "order"), true);

ALTER TABLE "order" ALTER COLUMN id SET DEFAULT nextval('order_id_seq');

insert into country (id, name)
values (40,	'Democratic Republic of Congo');

insert into nomenclature (id, name, country_id)
values (70,	'Астильба',	40);

insert into "user" (id, name, type, password)
values (1, 'Ангелина Альбертовна Матвеева', 'администратор', 'pass1');

insert into "user" (id, name, type, password)
values (2, 'Тестовый Продавец', 'продавец', 'pass2');

insert into "user" (id, name, type, password)
values (62,	'Агафонов Никандр Ефимович', 'кладовщик', 'pass62');

insert into counterpart (id, name, type, legal_status, legal_address, contact_person, phone)
values (1, 'Индивидуальный предприниматель ЗАО «Кузнецов Королев»','покупатель', 'Индивидуальный предприниматель',
'{"city": "г. Кизилюрт", "office": 100, "region": "Хабаровский край", "street": "пр. Элеваторный", "country": "Россия", "building": "64", "postal_code": "312588"}',
'Киселева Кира Кузьминична', '+78015838710');

insert into counterpart (id, name, type, legal_status, legal_address, contact_person, phone)
values (101, 'Юридическое лицо Международный центр', 'поставщик', 'Юридическое лицо',	
'{"city": "с. Байкальск", "office": 50, "region": "Забайкальский край", "street": "пер. 50 лет ВЛКСМ", "country": "Россия", "building": "1/8", "postal_code": "181111"}', 
'Тарас Игнатьевич Корнилов', '+75101419838');

insert into batch_of_products 
(id_product_batch, id_nomenclature, production_date, expiration_date, cost_price, amount, responsible, suppliers)
values (401, 70, '2025-04-17', '2026-04-17', 972.52, 26, 62, 101);

INSERT INTO storage_place (id, name, address) VALUES
(1, 'Склад 1', '{"street": "алл. Мая 1, д. 2 стр. 8", "city": "Москва", "postal_code": "379269", "country": "Россия", "coordinates": {"lat": "24.112643", "lng": "61.011824"}, "metro": "Курская"}'),
(2, 'Склад 2', '{"street": "ш. Энергетиков, д. 6 стр. 8/8", "city": "Москва", "postal_code": "275444", "country": "Россия", "coordinates": {"lat": "64.0714675", "lng": "-85.134205"}, "metro": "Китай-город"}'),
(3, 'Склад 3', '{"street": "наб. Крымская, д. 73 стр. 7/4", "city": "Москва", "postal_code": "532322", "country": "Россия", "coordinates": {"lat": "-85.578581", "lng": "-137.537090"}, "metro": "Арбатская"}'),
(4, 'Склад 4', '{"street": "бул. Котовского, д. 799 стр. 6", "city": "Москва", "postal_code": "057318", "country": "Россия", "coordinates": {"lat": "-27.301843", "lng": "-161.552970"}, "metro": "Арбатская"}'),
(5, 'Склад 5', '{"street": "ш. Базарное, д. 49 стр. 15", "city": "Москва", "postal_code": "349226", "country": "Россия", "coordinates": {"lat": "39.841832", "lng": "133.461482"}, "metro": "Охотный ряд"}'),
(6, 'Склад 6', '{"street": "ш. Магистральное, д. 45 стр. 848", "city": "Москва", "postal_code": "253563", "country": "Россия", "coordinates": {"lat": "47.9467045", "lng": "37.972912"}, "metro": "Арбатская"}'),
(7, 'Склад 7', '{"street": "бул. Фадеева, д. 3/8", "city": "Москва", "postal_code": "867165", "country": "Россия", "coordinates": {"lat": "-3.9640715", "lng": "55.265769"}, "metro": "Китай-город"}'),
(8, 'Склад 8', '{"street": "ул. Высотная, д. 6/1 к. 1/2", "city": "Москва", "postal_code": "633621", "country": "Россия", "coordinates": {"lat": "12.5771165", "lng": "-52.539158"}, "metro": "Китай-город"}'),
(9, 'Склад 9', '{"street": "бул. Леонова, д. 534 стр. 2/6", "city": "Москва", "postal_code": "640824", "country": "Россия", "coordinates": {"lat": "-37.3589815", "lng": "-94.009903"}, "metro": "Охотный ряд"}'),
(10, 'Склад 10', '{"street": "пр. Ленский, д. 7 стр. 28", "city": "Москва", "postal_code": "044025", "country": "Россия", "coordinates": {"lat": "-64.556132", "lng": "147.442355"}, "metro": "Охотный ряд"}'),
(11, 'Склад 11', '{"street": "пер. Садовый, д. 9/3", "city": "Москва", "postal_code": "576359", "country": "Россия", "coordinates": {"lat": "30.6809115", "lng": "-77.776005"}, "metro": "Охотный ряд"}'),
(12, 'Склад 12', '{"street": "алл. Промышленная, д. 59", "city": "Москва", "postal_code": "648281", "country": "Россия", "coordinates": {"lat": "-82.4147435", "lng": "-21.033602"}, "metro": "Курская"}'),
(13, 'Склад 13', '{"street": "бул. Леваневского, д. 8/7 стр. 7", "city": "Москва", "postal_code": "874329", "country": "Россия", "coordinates": {"lat": "-1.310931", "lng": "-34.763769"}, "metro": "Арбатская"}'),
(14, 'Склад 14', '{"street": "пер. Новый, д. 9 к. 152", "city": "Москва", "postal_code": "896872", "country": "Россия", "coordinates": {"lat": "32.6149005", "lng": "44.158055"}, "metro": "Парк культуры"}'),
(15, 'Склад 15', '{"street": "бул. Смоленский, д. 6/4 к. 9", "city": "Москва", "postal_code": "180024", "country": "Россия", "coordinates": {"lat": "-12.724958", "lng": "44.568628"}, "metro": "Парк культуры"}'),
(16, 'Склад 16', '{"street": "наб. Тульская, д. 869 к. 28", "city": "Москва", "postal_code": "249173", "country": "Россия", "coordinates": {"lat": "-47.985412", "lng": "29.830207"}, "metro": "Арбатская"}'),
(17, 'Склад 17', '{"street": "наб. Ольховая, д. 2/5", "city": "Москва", "postal_code": "971533", "country": "Россия", "coordinates": {"lat": "-53.475596", "lng": "-74.477026"}, "metro": "Курская"}'),
(18, 'Склад 18', '{"street": "пр. Братский, д. 2/2 стр. 7/5", "city": "Москва", "postal_code": "141160", "country": "Россия", "coordinates": {"lat": "10.2580525", "lng": "111.307273"}, "metro": "Арбатская"}'),
(19, 'Склад 19', '{"street": "пр. Разина, д. 21 к. 3", "city": "Москва", "postal_code": "008612", "country": "Россия", "coordinates": {"lat": "10.951773", "lng": "81.791822"}, "metro": "Парк культуры"}'),
(20, 'Склад 20', '{"street": "наб. Геологов, д. 6", "city": "Москва", "postal_code": "036235", "country": "Россия", "coordinates": {"lat": "28.5196795", "lng": "179.293664"}, "metro": "Курская"}');

insert into product_in_stock (id, id_nomenclature, id_product_batch, amount, storage_place)
values (6052, 70, 401, 3, 19);

insert into price (id, id_nomenclature, selling_price, id_product_batch)
values (6052, 70, 3005.13, 401);





