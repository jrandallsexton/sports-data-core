-- Core memory
ALTER SYSTEM SET max_connections = 300;              -- OK on 32 GB (we'll still favor pooling)
ALTER SYSTEM SET shared_buffers = '8GB';             -- ~25% of RAM
ALTER SYSTEM SET effective_cache_size = '24GB';      -- ~3× shared_buffers
ALTER SYSTEM SET work_mem = '32MB';                  -- per sort/hash; be mindful of concurrency
ALTER SYSTEM SET maintenance_work_mem = '2GB';       -- for VACUUM/CREATE INDEX