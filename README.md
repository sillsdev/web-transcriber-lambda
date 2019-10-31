# web-transcriber-lambda
REST API for transcriber - hosted in AWS lambda function  

DEV:  https://9u6wlhwuha.execute-api.us-east-2.amazonaws.com/dev/api  
  ./build.ps1 in src dir  
  
QA:  https://ukepgrpe6l.execute-api.us-east-2.amazonaws.com/qa/api  
  ./buildqa.ps1 in src dir  

PROD: https://kg9bz1c7f9.execute-api.us-east-1.amazonaws.com/prod/api  
  ./buildprod.ps1 in src dir
  
ActivityStates  
CurrentUsers - Get Only, returns logged in user  
GroupMemberships  
Groups  
Integrations  
Mediafiles   
- Get:  standard db record  
- Get:  {id}/fileurl - will return a signed url to download S3 file in audiourl field  
- Get:  {id}/file - will download the file directly  
- Post: will create record and return the signed url to upload the file to S3 in audiourl field  
- Post: /file - expects record and file in FormFile 

OrganizationMemberships  
Organizations  
- Post: CurrentUser set as owner  

Passages  
PassageSections (Use sections post instead)  
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

Users  
