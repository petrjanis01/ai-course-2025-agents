-- Create Langfuse database if not exists
SELECT 'CREATE DATABASE langfusedb'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'langfusedb')\gexec
