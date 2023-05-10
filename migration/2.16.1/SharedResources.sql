drop table sharedresources;
create table sharedresources 
(id serial4 not null,
 passageid int4 not null,
 clusterid int4 null, --if set this is only shared with a cluster organization
 title text,
 description text,
 languagebcp47 text,
 termsofuse text,
 keywords text,
 artifactcategoryid int,
 datecreated timestamp NULL,
 dateupdated timestamp NULL,
 archived bool NOT NULL DEFAULT false,
 lastmodifiedby int4 NULL,
 lastmodifiedorigin text NOT NULL DEFAULT 'https://admin-dev.siltranscriber.org'::text,
 CONSTRAINT pk_sharedresources PRIMARY KEY (id));

CREATE INDEX ix_sharedresources_passageid ON public.sharedresources USING btree (passageid);
CREATE INDEX ix_sharedresources_keywords ON public.sharedresources USING btree (keywords);
CREATE INDEX ix_sharedresources_lastmodifiedorigin_idx ON public.sharedresources USING btree (lastmodifiedorigin, lastmodifiedby, dateupdated);

ALTER TABLE public.sharedresources ADD CONSTRAINT fk_sharedresources_lastmodifiedby FOREIGN KEY (lastmodifiedby) REFERENCES public.users(id) ON DELETE SET NULL;
--alter table sharedresources drop constraint fk_sharedresources_passage;
ALTER TABLE public.sharedresources ADD CONSTRAINT fk_sharedresources_passage FOREIGN KEY (passageid) REFERENCES public.passages(id) ON DELETE CASCADE;
ALTER TABLE public.sharedresources ADD CONSTRAINT fk_sharedresources_org FOREIGN KEY (clusterid) REFERENCES public.organizations(id) ON DELETE SET NULL;

grant all on sharedresources to transcriber;
grant all on sharedresources_id_seq to transcriber;

--drop table sharedresourcereferences;
create table sharedresourcereferences
(id serial4 not null,
 sharedresourceid int4 not null,
 book text,
 chapter int not null, --0 if you want it to show up for all chapters for this book
 verses text not null, --0 if you want it to show up for all verses in this chapter
	datecreated timestamp NULL,
	dateupdated timestamp NULL,
 archived bool NOT NULL DEFAULT false,	
	lastmodifiedby int4 NULL,
	lastmodifiedorigin text NOT NULL DEFAULT 'noorigin'::text,	 
 CONSTRAINT pk_sharedresourcereferences PRIMARY KEY (id));

CREATE INDEX ix_sharedresourcereference_resource ON public.sharedresourcereferences USING btree (sharedresourceid);
CREATE INDEX ix_sharedresourcereference_reference ON public.sharedresourcereferences USING btree (book, chapter, verses);

ALTER TABLE public.sharedresourcereferences ADD CONSTRAINT fk_sharedresourcereference_lastmodifiedby FOREIGN KEY (lastmodifiedby) REFERENCES public.users(id) ON DELETE SET NULL;
ALTER TABLE public.sharedresourcereferences ADD CONSTRAINT fk_sharedresourcereference_resource FOREIGN KEY (sharedresourceid) REFERENCES public.sharedresources(id) ON DELETE CASCADE;

grant all on sharedresourcereferences to transcriber;
grant all on sharedresourcereferences_id_seq to transcriber;

--CLUSTER
alter table organizations add clusterbase bool not null default false;
alter table organizations add clusterid int4 null;

SELECT public.create_archive_rules();
select max(id) from passagestatechanges p 
