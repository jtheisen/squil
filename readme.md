# SQuiL

This is the source repo of the SQuiL prototype - a proof-of-concept for a generic SQL UI.

For a quick look at the thing, use the public demo hosting:

[Live demo of this prototype](https://squil.azurewebsites.net)

If you have a database with proper foreign key constraints between the tables, you may want
to check out how it looks in SQuiL. It's easy to install with the docker image, see the
section below.

If you are interested in the vision behind this prototype, I've written something about
that and myself on a dedicated site:

[Introduction](https://squil.net)

<!-- [![Build Status](https://dev.azure.com/bliptech/Squil/_apis/build/status/jtheisen.squil?branchName=master)](https://dev.azure.com/bliptech/Squil/_build/latest?definitionId=14&branchName=master) -->

## Install and use the docker image

First, you need Docker installed. If you're unfamiliar with that, just go to
[Docker](https://www.docker.com/get-started) and install Docker Desktop for your
operating system. It comes with a user interface to start and stop created
containers, but the container creation itself should be done in a terminal.

(If you don't use Docker for anything else you may want to go to its settings and
disable the automatic startup on login. Docker obviously consumes significant resources.)

Now install or update the SQuiL image with

    docker pull squiltech/squil

This pulls the image into a user-wide storage that you can also browse in
the Docker UI.

Before you then create a container instance from this image, first create an
environment variables file defining your database connections. The file's name
and location doesn't matter, and the contents should look like this:

    Connections__0__Name=AdventureWorksLT2017
    Connections__0__LongName=AdventureWorks 2017 Light
    Connections__0__ConnectionString=Server=host.docker.internal;Initial Catalog=AdventureWorksLT2017;User ID=squil;Password=qwerty;TrustServerCertificate=False;Connection Timeout=30;
    Connections__0__Description=Microsoft's AdventureWorks database is the official example database for Microsoft SQL Server.

Replace the values accordingly.

The format comes from the way ASP.NET expects list settings in environment variables by default.
You can add multiple connections by replacing the zero with increasing integers.

Then, you can create and run a container:

     docker run -d -p 8080:80 --env-file <environment-file> --name squil squiltech/squil

This will create and run a container named *squil* that can be started and stopped and uses
the connections defined in the environment file (which won't be used any more after that).
When running, it listens to port 8080, so the app will be at http://localhost:8080.

## Connect to a server on your local machine

Docker containers run technically on a different machine (from the OS perspective), so neither Windows authentication nor connecting via pipes (the default) works.

For SQuiL running in docker to connect to your local SQL Server instance, you

- need to enable TCP in your SQL Server,
- must refer to *host.docker.internal* (rather than *localhost*) as the server hostname in your connection strings and
- must use SQL Server authentication (rather than Windows authentication) and thus likely also create a SQL Server *login*.

The setting to enable TCP is in the SQL Server Connection Manager, which is a bit hidden. See
[this blog post](https://www.mytecbits.com/microsoft/sql-server/where-is-sql-server-configuration-manager)
to find it and
[this Stack Overflow answer](<https://stackoverflow.com/a/50170217/870815>)
to locate the setting once you found the manager.

Since Windows authentication can't be used here either, you need to create a login that uses
SQL Server authentication with username and password. This can be done with SQL Server Management Studio
by right-clicking on the *connection*/Security/Logins node in the object tree and selecting *New Login*. The relevant tabs here are *General* and *User Mapping*

Give that login a name, check *SQL Server authentication* and set a password. Then, under *User Mapping*, give the login a user mapping to the relevant databases with the `db_datareader` role checked for each such database in the list of roles below. You can then use that user/password combination with the server name *host.docker.internal* in your connection strings.
