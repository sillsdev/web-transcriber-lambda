# web-transcriber-lambda
REST API for transcriber - hosted in AWS lambda function  

DEV:  https://2e0azjfrgi.execute-api.us-east-1.amazonaws.com/dev/api  
  ./build.ps1 in src dir  
  
QA:  https://ktiyfgd6cj.execute-api.us-east-1.amazonaws.com/qa/api 
  ./buildqa.ps1 in src dir  

PROD: https://kg9bz1c7f9.execute-api.us-east-1.amazonaws.com/prod/api  
  ./buildprod.ps1 in src dir
  
PROD: https://kg9bz1c7f9.execute-api.us-east-1.amazonaws.com/prod/api
  ./buildprod.ps1 in src dir  
  

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
	since/{datetime} (anonymous)
Users  

+
+
+To run locally from the transcriber app:
+Run the api (with Code or VS) and note the host address.  You'll get an error when it first starts but ignore that (there isn't an endpoint at the starting url)
+replace the REACT_APP_HOST in env.development.local with the local url 
+REACT_APP_HOSTx=https://2e0azjfrgi.execute-api.us-east-1.amazonaws.com/dev
+REACT_APP_HOST=https://localhost:44370
