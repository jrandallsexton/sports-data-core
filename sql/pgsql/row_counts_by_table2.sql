DO
$$
DECLARE
    r RECORD;
    row_count BIGINT;
    total_rows BIGINT := 0;
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

        total_rows := total_rows + row_count;
    END LOOP;

    RAISE NOTICE 'Total Row Count | %', total_rows;
END;
$$;
