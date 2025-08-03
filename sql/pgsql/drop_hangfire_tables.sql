DO
$$
DECLARE
    obj RECORD;
BEGIN
    -- Drop foreign key constraints in hangfire schema
    FOR obj IN
        SELECT conname, c.relname
        FROM pg_constraint p
        JOIN pg_class c ON p.conrelid = c.oid
        JOIN pg_namespace n ON c.relnamespace = n.oid
        WHERE p.contype = 'f'
          AND n.nspname = 'hangfire'
    LOOP
        EXECUTE format('ALTER TABLE hangfire.%I DROP CONSTRAINT %I', obj.relname, obj.conname);
    END LOOP;

    -- Drop all tables in hangfire schema
    FOR obj IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'hangfire'
    LOOP
        EXECUTE format('DROP TABLE IF EXISTS hangfire.%I CASCADE', obj.tablename);
    END LOOP;
END;
$$;
