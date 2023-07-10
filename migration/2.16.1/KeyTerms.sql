--drop table orgkeyterms cascade;

create table orgkeyterms 
(id serial4 not null,
 organizationid int4 not null,
 term text not null,
 domain text,
 definition text,
 category text,
 	datecreated timestamp NULL,
	dateupdated timestamp NULL,
	archived bool NOT NULL DEFAULT false,
	lastmodifiedby int4 NULL,
	lastmodifiedorigin text NOT NULL DEFAULT 'https://admin-dev.siltranscriber.org'::text,
	offlineid text,
 CONSTRAINT pk_orgkeyterms PRIMARY KEY (id));

CREATE INDEX ix_orgkeyterms_orgid ON public.orgkeyterms USING btree (organizationid, term);
CREATE INDEX ix_orgkeyterms_lastmodifiedorigin_idx ON public.orgkeyterms USING btree (lastmodifiedorigin, lastmodifiedby, dateupdated);

ALTER TABLE public.orgkeyterms ADD CONSTRAINT fk_orgkeyterms_lastmodifiedby FOREIGN KEY (lastmodifiedby) REFERENCES public.users(id) ON DELETE SET NULL;
--alter table public.orgkeyterms drop constraint fk_orgkeyterms_organization;
ALTER TABLE public.orgkeyterms ADD CONSTRAINT fk_orgkeyterms_organization FOREIGN KEY (organizationid) REFERENCES public.organizations(id) ON DELETE CASCADE;

grant all on orgkeyterms to transcriber;
grant all on orgkeyterms_id_seq to transcriber;
-- DROP RULE rule_archivenotdelete ON public.orgkeyterms;

CREATE RULE rule_archivenotdelete AS
    ON DELETE TO public.orgkeyterms DO INSTEAD  UPDATE orgkeyterms SET archived = true, dateupdated = timezone('UTC'::text, CURRENT_TIMESTAMP)
  WHERE (orgkeyterms.id = old.id);
 
--drop table orgkeytermtargets;
create table orgkeytermtargets (
	id serial4 not null,
	organizationid int4 not null,
    term text null,  -- the org specific words will be here
    termindex int null, --the standard words will use this
    target text, 
	mediafileid int4 null,
 	datecreated timestamp NULL,
	dateupdated timestamp NULL,
	archived bool NOT NULL DEFAULT false,
	lastmodifiedby int4 NULL,
	lastmodifiedorigin text NOT NULL DEFAULT 'https://admin-dev.siltranscriber.org'::text,
	offlineid text,
	offlinemediafileid text,
 	CONSTRAINT pk_orgkeytermtargets PRIMARY KEY (id)	
	);
CREATE INDEX ix_orgkeytermtargets_orgterm ON public.orgkeytermtargets USING btree (organizationid, term);
CREATE INDEX ix_orgkeytermtargets_orgindex ON public.orgkeytermtargets USING btree (organizationid, termindex);
CREATE INDEX ix_orgkeytermtargets_lastmodifiedorigin_idx ON public.orgkeytermtargets USING btree (lastmodifiedorigin, lastmodifiedby, dateupdated);

	
CREATE RULE rule_archivenotdelete AS
    ON DELETE TO public.orgkeytermtargets DO INSTEAD  UPDATE orgkeytermtargets SET archived = true, dateupdated = timezone('UTC'::text, CURRENT_TIMESTAMP)
  WHERE (orgkeytermtargets.id = old.id);

ALTER TABLE public.orgkeytermtargets ADD CONSTRAINT fk_orgkeytermtargets_lastmodifiedby FOREIGN KEY (lastmodifiedby) REFERENCES public.users(id) ON DELETE set NULL;
--alter table public.orgkeytermtargets drop constraint fk_orgkeytermtargets_organization;
ALTER TABLE public.orgkeytermtargets ADD CONSTRAINT fk_orgkeytermtargets_organization FOREIGN KEY (organizationid) REFERENCES public.organizations(id) ON DELETE CASCADE;

grant all on orgkeytermtargets to transcriber;
grant all on orgkeytermtargets_id_seq to transcriber;


-- drop table orgkeytermreferences;
create table orgkeytermreferences
(id serial4 not null,
 orgkeytermid int4 not null,
 projectid int not null,
 sectionid int not null,
 	datecreated timestamp NULL,
	dateupdated timestamp NULL,
	archived bool NOT NULL DEFAULT false,	
	lastmodifiedby int4 NULL,
	lastmodifiedorigin text NOT NULL DEFAULT 'noorigin'::text,	 
 offlineid text,
 CONSTRAINT pk_orgkeytermreferences PRIMARY KEY (id));

CREATE INDEX ix_orgkeytermreference_orgkeytermid ON public.orgkeytermreferences USING btree (orgkeytermid);
CREATE INDEX ix_orgkeytermreference_reference ON public.orgkeytermreferences USING btree (projectid, sectionid);

ALTER TABLE public.orgkeytermreferences ADD CONSTRAINT fk_orgkeytermreference_lastmodifiedby FOREIGN KEY (lastmodifiedby) REFERENCES public.users(id) ON DELETE SET NULL;
ALTER TABLE public.orgkeytermreferences ADD CONSTRAINT fk_orgkeytermreference_orgkeyterm FOREIGN KEY (orgkeytermid) REFERENCES public.orgkeyterms(id) ON DELETE CASCADE;
ALTER TABLE public.orgkeytermreferences ADD CONSTRAINT fk_orgkeytermreference_project FOREIGN KEY (projectid) REFERENCES public.projects(id) ON DELETE CASCADE;
ALTER TABLE public.orgkeytermreferences ADD CONSTRAINT fk_orgkeytermreference_section FOREIGN KEY (sectionid) REFERENCES public.sections(id) ON DELETE CASCADE;

grant all on orgkeytermreferences to transcriber;
grant all on orgkeytermreferences_id_seq to transcriber;



