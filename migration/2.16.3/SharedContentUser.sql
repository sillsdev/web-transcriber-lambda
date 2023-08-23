alter table users add sharedcontentadmin bool;
alter table users add sharedcontentcreator bool;
update projects set ispublic = false, dateupdated = current_timestamp at time zone 'utc',lastmodifiedorigin = 'migration2.16.3' where ispublic = true

update users set sharedcontentadmin = true, sharedcontentcreator=true, dateupdated = current_timestamp at time zone 'utc', lastmodifiedorigin ='release' where email = 'nathan_payne@sil.org'