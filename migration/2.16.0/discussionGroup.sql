alter table discussions add groupid int CONSTRAINT discussions_group_fk REFERENCES groups(id)   -- if x (a) doens't exist, this will fail!
ON UPDATE CASCADE ON DELETE set NULL;  

ALTER TABLE public.sectionresources ADD CONSTRAINT sectionresources_passage_fk FOREIGN KEY (passageid) REFERENCES passages(id) ON DELETE CASCADE;
ALTER TABLE public.sectionresources ADD CONSTRAINT sectionresources_project_fk FOREIGN KEY (projectid) REFERENCES projects(id) ON DELETE CASCADE;

ALTER TABLE public.sectionresourceusers ADD CONSTRAINT sectionresourceusers_user_fk FOREIGN KEY (userid) REFERENCES users(id) ON UPDATE CASCADE ON DELETE SET NULL;
