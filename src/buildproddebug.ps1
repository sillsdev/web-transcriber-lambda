﻿dotnet build
dotnet lambda package --configuration release --output-package bin/release/net8.0/deploy-package_prodDBG.zip
serverless deploy --verbose -s prodDBG
