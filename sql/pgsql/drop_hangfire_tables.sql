DO
$$
DECLARE
    obj RECORD;
BEGIN
    -- Drop foreign key constraints in hangfire schema
    FOR obj IN
        SELECT conname, conrelid::regclass
        FROM pg_constraint
        WHERE contype = 'f'
          AND connamespace = 'hangfire'::regnamespace
    LOOP
        EXECUTE format('ALTER TABLE hangfire.%s DROP CONSTRAINT %I', obj.conrelid::text, obj.conname);
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
