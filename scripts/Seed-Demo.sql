-- Fixtures locais: não acessa feeds nem representa notícias reais.
-- Execute somente em uma base descartável chamada coreletter_demo*.
DO $$
BEGIN
    IF current_database() NOT LIKE 'coreletter_demo%' THEN
        RAISE EXCEPTION 'Use exclusivamente uma base descartável coreletter_demo*';
    END IF;
END $$;

INSERT INTO "Stories" ("Id", "Title", "Topic", "Status", "ItemCount", "SourceCount", "FirstPublishedAt", "LastPublishedAt", "UpdatedAt", "Attempts", "Version")
SELECT ('11111111-1111-4111-8111-' || lpad(n::text, 12, '0'))::uuid,
       'DEMO FICTÍCIA ' || n || ': Aurora apresenta projeto de pesquisa',
       'ciencia', 'agrupada', 2, 2, now(), now() - n * interval '1 minute', now(), 0, 1
FROM generate_series(1, 8) AS n
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "FeedItems" ("Id", "SourceId", "SourceName", "Topic", "Url", "Title", "Summary", "PublishedAt", "CollectedAt", "Status", "Flags", "StoryId")
SELECT ('22222222-2222-4222-8222-' || lpad((n * 10 + s)::text, 12, '0'))::uuid,
       'demo-' || s, 'Fonte fictícia ' || s, 'ciencia',
       'https://fonte' || s || '.example.invalid/demo/' || n,
       'Aurora apresenta projeto de pesquisa ' || n || ' com ' || (s * 100) || ' participantes',
       'Conteúdo inteiramente fictício para ensaio. Aurora apresenta projeto de pesquisa com ' || (s * 100) || ' participantes.',
       now() - n * interval '1 minute', now(), 'triado', '',
       ('11111111-1111-4111-8111-' || lpad(n::text, 12, '0'))::uuid
FROM generate_series(1, 8) AS n CROSS JOIN generate_series(1, 2) AS s
ON CONFLICT ("Url") DO NOTHING;
