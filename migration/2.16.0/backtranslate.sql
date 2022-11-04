INSERT INTO public.artifacttypes
(organizationid, typename, datecreated, dateupdated, lastmodifiedby, archived, lastmodifiedorigin)
VALUES(null, 'wholebacktranslation', current_timestamp, current_timestamp, null, false, 'setup'::text);

update mediafiles set artifacttypeid = (select id from artifacttypes where typename = 'wholebacktranslation'), dateupdated = current_timestamp, lastmodifiedorigin = 'setup'
where artifacttypeid = (select id from artifacttypes where typename = 'backtranslation') and sourcesegments is null