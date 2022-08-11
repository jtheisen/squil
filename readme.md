# SQuiL

This is the source repo of the SQuiL prototype - a proof-of-concept for a generic SQL UI.

For a quick look at the thing, use the public demo hosting:

[Live demo of this prototype](https://squil.azurewebsites.net)

If you have a database with proper foreign key contraints between the tables, you may want
to check out how it looks in SQuiL. It's easy to install with the docker image, see the
section below.

If you are interested in the vision behind this prototype, I've written something about
that and myself on a dedicated site:

[Introduction](https://squil.net)

<!-- [![Build Status](https://dev.azure.com/bliptech/Squil/_apis/build/status/jtheisen.squil?branchName=master)](https://dev.azure.com/bliptech/Squil/_build/latest?definitionId=14&branchName=master) -->

## Install and use the docker image

First, you need Docker installed. When you're unfamiliar with it, just go to
[Docker](https://www.docker.com/get-started) and install Docker Desktop for your
operating system. It comes with a user interface to start and stop installed
containers, but the installation itself should be done in a terminal.

Install or update the SQuiL image with

    docker pull squiltech/squil

Before you create a container instance with a run command, create an environment variables
file defining your connections. The name doesn't matter and it should look like this:

    Connections__0__Name=AdventureWorksLT2017
    Connections__0__LongName=AdventureWorks 2017 Light
    Connections__0__ConnectionString=Server=host.docker.internal;Initial Catalog=AdventureWorksLT2017;User ID=squil;Password=qwerty;TrustServerCertificate=False;Connection Timeout=30;
    Connections__0__Description=Microsoft's AdventureWorks database is the official example database for Microsoft SQL Server.

Replace the values accordingly

The format comes from the way ASP.NET expects lists in environment variables by default.
You can add multiple connections by replacing the zero with increasing integers.

Then, you can "install" and run the container:

     docker run -d -p 8080:80 --env-file <environment-file> --name squil squiltech/squil

This will create and run a container named *squil* that can be started and stopped and uses
the connections defined in the environment file (which won't be used any more after than).
When running, it listens to port 8080, so the app will be at http://localhost:8080.

## Connect to a server on your local machine

Docker containers run technically on a different machine (from the OS perspective), so neither Windows authentication nor connecting via pipes (the default) works.

For SQuiL running in docker to connect to your local SQL Server instance, you

- need to enable TCP in your SQL Server,
- can refer to host.docker.internal as the server hostname in your connection strings and
- must use SQL Server authentication (rather than Windows authentication) and thus create a *login*.

The setting to enable TCP is in the SQL Server Connection Manager, which is a bit hidden. See
[this blog post](https://www.mytecbits.com/microsoft/sql-server/where-is-sql-server-configuration-manager)
to find it and
[this Stack Overflow answer](<https://stackoverflow.com/a/50170217/870815>)
to locate the setting is once you found the manager.

Since Windows authentication can't be used here either, you need to create a login that uses
SQL Server authentication with username and password. Give that login a user assignments to
the relevant databases with the `db_datareader` role,
