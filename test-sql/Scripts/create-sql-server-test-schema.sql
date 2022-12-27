use [master]

DECLARE @kill varchar(8000) = '';  
SELECT @kill = @kill + 'kill ' + CONVERT(varchar(5), req_spid) + ';'  
FROM master.dbo.syslockinfo
WHERE rsc_type = 2
AND rsc_dbid  = db_id('squil-test')

EXEC(@kill);
go

drop database if exists [squil-test] 
create database [squil-test] collate Latin1_General_100_CI_AS_SC_UTF8
go

use [squil-test]
go

create schema duplicates

create table PrincipalWithDuplicates (
	id int not null primary key,
	name varchar(40) not null constraint AK_Name unique
)

create table Dependent (
	id int not null primary key,
	name varchar(40) not null references PrincipalWithDuplicates (name)
)
go

alter index AK_Name on duplicates.PrincipalWithDuplicates disable
alter table duplicates.PrincipalWithDuplicates nocheck constraint all
alter table duplicates.Dependent nocheck constraint all

insert into duplicates.PrincipalWithDuplicates values (1, 'foo'), (2, 'foo'), (3, 'bar')
insert into duplicates.Dependent values (1, 'foo'), (2, 'bar')

-- Tables with different primary keys

create table KeyedOnIdentity(
	id int not null identity primary key,
	name varchar(10) not null constraint AK_KeyedOnIdentity unique,
	upperCaseName as upper(name),
	payload varchar(40) null
)

insert into KeyedOnIdentity(name)
values ('one'), ('two'), ('three')

create table KeyedOnName(
	name varchar(10) not null primary key,
	upperCaseName as upper(name),
	payload varchar(40) null
)

insert into KeyedOnName(name)
values ('one'), ('two'), ('three')

create table KeyedOnDefaultingUuid(
	id uniqueidentifier not null primary key default (newid()),
	name varchar(10) not null constraint AK_KeyedOnDefaultingUuid unique,
	upperCaseName as upper(name),
	payload varchar(40) null
)

insert into KeyedOnDefaultingUuid(name)
values ('one'), ('two'), ('three')

create table KeyedOnNondefaultingUuid(
	id uniqueidentifier not null primary key,
	name varchar(10) not null constraint AK_KeyedOnNondefaultingUuid unique,
	upperCaseName as upper(name),
	payload varchar(40) null
)

insert into KeyedOnNondefaultingUuid(id, name)
values (newid(), 'one'), (newid(), 'two'), (newid(), 'three')
