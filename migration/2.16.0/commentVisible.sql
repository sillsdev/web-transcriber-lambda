alter table comments add visible jsonb;

alter table sections add groupid int references groups (id);
alter table orgworkflowsteps add groupid int references groups (id);