-- Active: 1751184123209@@127.0.0.1@5432@sdProducer.FootballNcaa
DO
$$
DECLARE
    r RECORD;
    row_count BIGINT;
BEGIN
    RAISE NOTICE 'Table Name | Row Count';
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
        ORDER BY table_name
    LOOP
        EXECUTE format(
            'SELECT COUNT(*) FROM %I.%I',
            r.table_schema,
            r.table_name
        )
        INTO row_count;

        IF row_count > 0 THEN
            RAISE NOTICE '% | %', r.table_name, row_count;
        END IF;
    END LOOP;
END;
$$;
