pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DEPLOY_SHARE = '\\\\10.0.10.56\\cdn'
        REMOTE_USER  = '10.0.10.56\\mbweb'
        REMOTE_PASS  = 'NgaKZ&GZK8@3&6yG6F@f6gr!'
        PROJECT      = 'src\\CdnFileService.Web\\CdnFileService.Web.csproj'
        PUBLISH_DIR  = 'src\\CdnFileService.Web\\bin\\publish'
    }

    stages {
        stage('Restore') {
            steps {
                bat '''
                    set MSBuildSDKsPath=C:\\Program Files\\dotnet\\sdk\\9.0.312\\Sdks
                    dotnet restore %PROJECT%
                '''
            }
        }

        stage('Build & Publish') {
            steps {
                bat '''
                    set MSBuildSDKsPath=C:\\Program Files\\dotnet\\sdk\\9.0.312\\Sdks
                    dotnet publish %PROJECT% -c Release -o %PUBLISH_DIR% --no-restore
                '''
            }
        }

        stage('Deploy') {
            steps {
                bat '''
                    rem Drop any stale handle, then connect fresh - abort if the share cannot be reached
                    net use %DEPLOY_SHARE% /delete /y >nul 2>&1
                    net use %DEPLOY_SHARE% /user:%REMOTE_USER% "%REMOTE_PASS%" /persistent:no
                    IF ERRORLEVEL 1 (
                        echo ERROR: Cannot connect to %DEPLOY_SHARE%. Verify the share exists on the server and %REMOTE_USER% has write access.
                        EXIT /B 1
                    )

                    rem Put app offline - IIS will unload the app automatically
                    echo ^<html^>^<body^>Updating...^</body^>^</html^> > %DEPLOY_SHARE%\\app_offline.htm

                    rem Wait for IIS to release files
                    ping 127.0.0.1 -n 4 >nul

                    rem Copy files (mirror). Preserve server configs, runtime logs, and - critically -
                    rem the shared CDN content under Storage / App_Data so a deploy never wipes assets.
                    robocopy %PUBLISH_DIR% %DEPLOY_SHARE% /MIR /XF appsettings.json appsettings.Development.json appsettings.Production.json web.config app_offline.htm /XD logs Storage App_Data /NFL /NDL /NP /R:3 /W:5
                    IF %ERRORLEVEL% GEQ 8 (
                        del %DEPLOY_SHARE%\\app_offline.htm >nul 2>&1
                        net use %DEPLOY_SHARE% /delete /y >nul 2>&1
                        EXIT /B 1
                    )

                    rem Remove app_offline.htm - IIS will restart the app (which auto-applies EF migrations)
                    del %DEPLOY_SHARE%\\app_offline.htm

                    rem Disconnect
                    net use %DEPLOY_SHARE% /delete /y >nul 2>&1
                    EXIT /B 0
                '''
            }
        }
    }

    post {
        success {
            echo 'Deployment SUCCESS - Cdn-File-Service updated on \\\\10.0.10.56\\cdn'
        }
        failure {
            bat '''
                del %DEPLOY_SHARE%\\app_offline.htm >nul 2>&1
                net use %DEPLOY_SHARE% /delete /y >nul 2>&1
            '''
            echo 'Deployment FAILED!'
        }
    }
}
