use [master]
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

