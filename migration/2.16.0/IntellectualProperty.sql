--drop table intellectualpropertys;
create table intellectualpropertys (
    id serial primary key,
	organizationid int not null references organizations(id) on delete cascade on update cascade,
	rightsholder	text not null,
	releasemediafileid int references mediafiles(id) on delete CASCADE on update CASCADE,
	notes text null,
    offlineid text,
    offlinemediafileid text,
	datecreated timestamp not null,
	dateupdated timestamp not null,
	lastmodifiedby int null references users(id) on delete set null on update cascade,
	lastmodifiedorigin text,
	archived bool not null default FALSE
	);
grant all on intellectualpropertys to transcriber;
grant all on intellectualpropertys_id_seq to transcriber;


create trigger archivetrigger after
update
    on
    public.intellectualpropertys for each row execute function archive_trigger();

-- Table Rules

-- DROP RULE rule_archivenotdelete ON public.intellectualpropertys;

CREATE RULE rule_archivenotdelete AS
    ON DELETE TO public.intellectualpropertys DO INSTEAD  UPDATE intellectualpropertys SET archived = true, dateupdated = timezone('UTC'::text, CURRENT_TIMESTAMP)
  WHERE (intellectualpropertys.id = old.id);


