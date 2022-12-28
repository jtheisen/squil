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

create table RelatedPrincipal (
	id int not null identity constraint PK_Principal primary key,
	[name] varchar(10) not null constraint AK_Principal_Name unique
)

create table RelatedDependent (
	id int not null identity constraint PK_Dependent primary key,
	principalId int not null references RelatedPrincipal (id),
	[name] varchar(10) not null constraint AK_Dependent_Name unique,

	index IX_PrincipalId (principalId, [name])
)
go

alter table RelatedPrincipal nocheck constraint all
alter table RelatedDependent nocheck constraint all

insert into RelatedPrincipal([name]) values ('one'), ('two'), ('three')
insert into RelatedDependent(principalId, [name]) values (1, 'foo'), (2, 'bar'), (3, 'baz')

-- Tables with different primary keys

create table NoEditableFields(
	id int not null identity constraint PK_NoEditableFields primary key,
)

create table KeyedOnIdentity(
	id int not null identity constraint PK_KeyedOnIdentity primary key,
	name varchar(10) not null constraint AK_KeyedOnIdentity unique,
	upperCaseName as upper(name),
	payload varchar(40) null
)

insert into KeyedOnIdentity(name)
values ('one'), ('two'), ('three')

create table KeyedOnName(
	name varchar(10) not null constraint PK_KeyedOnName primary key,
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
