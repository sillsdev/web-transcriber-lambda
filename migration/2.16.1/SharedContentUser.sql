alter table users add sharedcontentadmin bool;
alter table users add sharedcontentcreator bool;
update projects set ispublic = false;

update users set sharedcontentadmin = true, sharedcontentcreator=true, dateupdated = current_timestamp at time zone 'utc', lastmodifiedorigin ='test' where email = 'nathan_payne@sil.org'