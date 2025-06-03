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

COPY storage_place FROM 'D:\bmstu\PPO\software_design\FlowerShop\Integration.Tests\storage_places.csv' (DELIMITER ';', ENCODING 'UTF8');

insert into product_in_stock (id, id_nomenclature, id_product_batch, amount, storage_place)
values (6052, 70, 401, 3, 19);

insert into price (id, id_nomenclature, selling_price, id_product_batch)
values (6052, 70, 3005.13, 401);





