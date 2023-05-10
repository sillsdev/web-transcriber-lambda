with memberid as (
	select id from roles where description = 'Member'
),
adminid as (
	select id from roles where description = 'Admin'
),
orgmems as (
select distinct om.id, adminid.id as adminid,  om.userid, u.name, o.name 
from organizationmemberships om
inner join memberid on om.roleid = memberid.id
inner join groupmemberships gm on om.userid = gm.userid 
inner join groups g on g.id = gm.groupid and g.ownerid = om.organizationid 
inner join adminid on gm.roleid = adminid.id
inner join users u on u.id = om.userid
inner join organizations o on o.id = om.organizationid order by o.name
) --select * from orgmems
update organizationmemberships 
set roleid = adminid, dateupdated = current_timestamp at time zone 'utc', lastmodifiedorigin = 'migration2.16.3'
from orgmems
where orgmems.id = organizationmemberships.id
