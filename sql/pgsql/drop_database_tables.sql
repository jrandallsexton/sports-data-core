-- Active: 1751184123209@@127.0.0.1@5432@sdProducer.FootballNcaa.Hangfire
DO
$$
DECLARE
    obj RECORD;
BEGIN
    -- Drop all foreign key constraints
    FOR obj IN
        SELECT conname, conrelid::regclass
        FROM pg_constraint
        WHERE contype = 'f'
    LOOP
        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', obj.conrelid, obj.conname);
    END LOOP;

    -- Drop all tables in the current schema(s)
    FOR obj IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
    LOOP
        EXECUTE format('DROP TABLE IF EXISTS %I CASCADE', obj.tablename);
    END LOOP;
END;
$$;
