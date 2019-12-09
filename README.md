# web-transcriber-lambda
REST API for transcriber - hosted in AWS lambda function  

DEV:  https://9u6wlhwuha.execute-api.us-east-2.amazonaws.com/dev/api  
  ./build.ps1 in src dir  
  
QA:  https://ukepgrpe6l.execute-api.us-east-2.amazonaws.com/qa/api  
  ./buildqa.ps1 in src dir  

All Controllers have a GET route since/{datetime}:
i.e.  api/passages/since/2019-12-06T20:00:35
returns all records created or modified since the provided datetime

ActivityStates  
CurrentUsers - Get Only, returns logged in user  
GroupMemberships  
Groups  
Integrations  
Invitations
Mediafiles   
- Get:  standard db record  
- Get:  {id}/fileurl - will return a signed url to download S3 file in audiourl field  
- Get:  {id}/file - will download the file directly  
- Post: will create record and return the signed url to upload the file to S3 in audiourl field  
- Post: /file - expects record and file in FormFile 
Called from s3 trigger
- Get:  fromfile/{s3file} - return mediafile associated with s3 filename
- Patch: {id}/fileinfo/{filesize}/{duration} update filesize and duration only

OrganizationMemberships  
Organizations  
- Post: CurrentUser set as owner  

Passages  
PassageSections (Use sections post instead)  
PassageStateChanges
Plans  
Plantypes  
Projectintegrations  
Projects  
Projecttypes  
Roles  
S3Files  
- Get: List files  
- Get: {filename} - download file  
- Get: {folder/filename} - download file  
- Post: upload file in FormFile  
- Del:  {filename}  

Sections   
- Get: {Id}/assignments - return Assignments {User, Role}  
- Post: {Id}/{role}/{userId} i.e. sections/356/Reviewer/2  will assign all passages in section 356 to user 2 as Reviewer  
- Del:  {Id}/{role}          i.e. sections/356/Reviewer will remove all reviewer assignments  

Statehistory (VwPassageStateHistoryEmail view)
	Get since only
Users  
