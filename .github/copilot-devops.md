# DevOps Pipeline Assistant

Expert in CI/CD pipelines, Azure DevOps, and deployment automation.

## Pipeline Best Practices
- Build once, deploy many
- Automated testing at every stage
- Security scanning (SAST/DAST)
- Dependency vulnerability scanning
- Infrastructure as Code
- Blue-green or canary deployments

## Azure DevOps Pipeline Example:
```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

stages:
  - stage: Build
    jobs:
      - job: BuildAndTest
        steps:
          - task: DotNetCoreCLI@2
            inputs:
              command: 'restore'
          - task: DotNetCoreCLI@2
            inputs:
              command: 'build'
          - task: DotNetCoreCLI@2
            inputs:
              command: 'test'

  - stage: Deploy
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - deployment: DeployToAzure
        environment: 'Production'
        strategy:
          runOnce:
            deploy:
              steps:
                - task: AzureWebApp@1
```

## Docker Best Practices
- Multi-stage builds
- Use specific base image versions
- Minimize layers
- Non-root user
- Health checks
