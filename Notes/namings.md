# SQL Server

Key names must be unique per schema (not database, not table).

Index names must be unique per table.

However, on each table, index names and key names can't collide either and all primary and alternate keys on a table are also appearing as an index with the same name. This index can't be deleted, but deleting the key also makes this index vanish.

That means that in SQL Server, keys really *are* indexes and in particular, a primary key thus defines a primary order, potentially different from the clustered order.

# MySQL

Like SQL Server, keys really are indexes - their names can't collide on a table and each key appears as an index as well. The only difference is that unlike SQL Server, MySQL allows dropping the index that is a key - but it drops the key along with it.

Both key and index names must be unique only to the table. The same names can be used on other tables.

MySQL doesn't have schemas, *CREATE SCHEMA* creates databases.

# Postgres

Again, names must not collide, but this time both keys and indexes must be uniquely named in the schema.

In fact, the *DROP INDEX* statement doesn't allow for an *ON* clause. Instead, the name of the index can be schema-qualified.

Strangly keys can't be defined with directions for the columns, even though an index is always created.

# Oracle

Oracle behaves exactly like Postgres, including the refusal to specify key column directions, the missing *ON* clause and that names must be unique within schemas.

Creating schemas and databases isn't quite as trivial and *USE [database]* doesn't exist at all. Creating schemas must go along with creating a user and creating databases is usually done with an external tool.
