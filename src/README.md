# Web Transcriber Lambda

REST API for the SIL Web Transcriber application - hosted as an AWS Lambda function.

## Overview

A .NET 8 web API that provides backend services for collaborative transcription and translation workflows. The API supports project management, media file processing, workflow orchestration, and offline data synchronization.

## Technology Stack

- **.NET 8.0** with C# 12.0
- **Entity Framework Core** for data access
- **JsonApiDotNetCore** for JSON:API specification compliance
- **AWS Lambda** for serverless hosting
- **PostgreSQL** with JSONB support
- **Amazon S3** for media file storage
- **Amazon SQS** for asynchronous processing

## Deployment Environments

### DEV
- **URL**: https://2e0azjfrgi.execute-api.us-east-1.amazonaws.com/dev/api
- **Deploy**: Run `./build.ps1` in src directory

### QA
- **URL**: https://ktiyfgd6cj.execute-api.us-east-1.amazonaws.com/qa/api
- **Deploy**: Run `./buildqa.ps1` in src directory

### PROD
- **URL**: https://kg9bz1c7f9.execute-api.us-east-1.amazonaws.com/prod/api
- **Deploy**: Run `./buildprod.ps1` in src directory

## Key Features

- **Project & Plan Management**: Organize transcription work into hierarchical projects and plans
- **Media File Processing**: Upload, process, and manage audio/video files with transcription metadata
- **Workflow Engine**: Configurable workflow steps and state management
- **Offline Support**: Full offline data synchronization with conflict resolution
- **Artifact Management**: Support for multiple artifact types and categories
- **External Integrations**: Integration with Aquifer and other external services
- **Team Collaboration**: User management, roles, and group-based permissions

## API Endpoints

### ActivityStates
Standard CRUD operations for workflow activity states.

### CurrentUsers
- **GET only**: Returns currently logged in user

### GroupMemberships
Manage user memberships in groups.

### Groups
Manage collaboration groups.

### Integrations
External service integrations.

### Invitations
Manage project and group invitations.

### Mediafiles
- **GET**: Standard database record
- **GET** `{id}/fileurl`: Returns a signed URL to download S3 file from audiourl field
- **GET** `{id}/file`: Downloads the file directly
- **POST**: Creates record and returns signed URL to upload file to S3 in audiourl field
- **POST** `/file`: Expects record and file in FormFile
- **GET** `fromfile/{s3file}`: Returns mediafile associated with S3 filename (called from S3 trigger)
- **PATCH** `{id}/fileinfo/{filesize}/{duration}`: Updates filesize and duration only

### OrganizationMemberships
Manage user memberships in organizations.

### Organizations
- **POST**: CurrentUser automatically set as owner

### Passages
Manage passage records for transcription units.

### PassageSections
*(Use sections endpoint instead)*

### PassageStateChanges
Track workflow state changes for passages.

### Plans
Manage transcription plans within projects.

### Plantypes
Define and manage plan types.

### Projectintegrations
Configure project-specific integrations.

### Projects
Manage transcription projects.

### Projecttypes
Define and manage project types.

### Roles
Manage user roles and permissions.

### S3Files
- **GET**: List files
- **GET** `{filename}`: Download file
- **GET** `{folder/filename}`: Download file with folder path
- **POST**: Upload file in FormFile
- **DELETE** `{filename}`: Delete file

### Sections
- **GET** `{Id}/assignments`: Returns assignments {User, Role}
- **POST** `{Id}/{role}/{userId}`: Assign passages (e.g., `sections/356/Reviewer/2` assigns all passages in section 356 to user 2 as Reviewer)
- **DELETE** `{Id}/{role}`: Remove all assignments for role (e.g., `sections/356/Reviewer` removes all reviewer assignments)

### SharedResources
**Shared Resources**:

Media is created in Plan A  Passage A1 Mediafile ASM1  
Sharedresource SR1 passageId = A1

Plan B links to shared internalization resource  
creates Mediafile BM1 in Plan B with **ResourcePassageId** = A1  - BM1 passageid may be set to internalization passage

**Shared Notes**:

Media is created in Plan A  Passage A1 Mediafile ASM1
Sharedresouce SR1 passageId = A1

Plan B links to resource in NOTE Passage B1
Passage B1 **SharedResourceId** = SR1
 
On import plan b to another database, first passage B1 with offlinesharedresourceid = SR1 takes ownership
	SR1.passageid = B1
	any media (like BM1) with resourepassageid = A1 is set to B1

### Statehistory
VwPassageStateHistoryEmail view
- **GET** `since/{datetime}`: Anonymous access to state history

### Users
Manage user accounts and profiles.

## Local Development

### Prerequisites
- .NET 8.0 SDK
- PostgreSQL database
- AWS CLI configured (optional, for S3 access)
- Visual Studio 2022 or VS Code

### Running Locally
1. Open the solution in Visual Studio or VS Code
2. Run the API (note the host address, typically `https://localhost:44370`)
3. You'll get an error at the starting URL - this is expected as there isn't an endpoint at the root

### Running with Transcriber Frontend
To connect the transcriber app to your local API:
1. Run the API and note the host address
2. Update `REACT_APP_HOST` in `env.development.local` of the transcriber app:
   ```
   REACT_APP_HOSTx=https://2e0azjfrgi.execute-api.us-east-1.amazonaws.com/dev
   REACT_APP_HOST=https://localhost:44370
   ```

### Configuration
Configure via `appsettings.json` or environment variables:
- Database connection string
- AWS credentials and region
- S3 bucket names
- SQS queue URLs

## Project Structure

```
src/
??? Controllers/         # API endpoint controllers
??? Models/              # Domain models and entities
??? Services/            # Business logic layer
??? Repositories/        # Data access layer
??? Data/                # Database context and migrations
??? Utility/             # Helper classes and extensions
??? serverless.yml       # AWS Lambda configuration
```

## Contributing

This project is maintained by SIL International. See the [GitHub repository](https://github.com/sillsdev/web-transcriber-lambda) for contribution guidelines.

## License

© SIL International

## Notes
**General Resources**:

Media is imported into in Plan A (ie a Chapter audio) AM1
Chunked Media is created AM2 with Projres segment set and **SourceMediaId** = AM1
SectionResource is created 

**Shared Resources**:

Media is created in Plan A  Passage A1 Mediafile ASM1  
Sharedresource SR1 passageId = A1

Plan B links to shared internalization resource  
creates Mediafile BM1 in Plan B with **ResourcePassageId** = A1  - BM1 passageid may be set to internalization passage


**Shared Notes**:

Media is created in Plan A  Passage A1 Mediafile ASM1
Sharedresouce SR1 passageId = A1

Plan B links to resource in NOTE Passage B1
Passage B1 **SharedResourceId** = SR1