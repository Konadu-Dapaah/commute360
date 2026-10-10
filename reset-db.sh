#!/bin/bash
#whole script immediately stops when a single command fails
set -e

echo "Dropping database..."
dotnet ef database drop --force

echo "Applying migrations..."
dotnet ef database update

echo "Done. Database is reset."