# infra/ — SalonBooking on AWS as code

`salonbooking.yml` is a CloudFormation template that creates the whole AWS setup in one stack: security groups, RDS Postgres, ElastiCache (Valkey), the S3 image bucket, the SQS queue, IAM roles, six Elastic Beanstalk environments, CloudFront in front of the Gateway, and the Amplify website (which builds with `amplify.yml` at the repo root).

## One-time setup

1. An IAM user with access keys (not the root user), then `aws configure` with region `ap-southeast-2`.
2. S3 → "Block Public Access settings for this account" turned off, so the image bucket can be public-read.
3. A GitHub personal access token with read access to the repository (Amplify uses it to pull the code).
4. An S3 bucket for the build zips, e.g. `aws s3 mb s3://salonbooking-artifacts-<something-unique>`.

## Deploy

Build and upload the six zips (each a `dotnet publish -c Release -r linux-x64 --self-contained false` output with a `Procfile` such as `web: dotnet Gateway.dll` at its root):

```bash
aws s3 cp Identity.Api.zip     s3://<artifact-bucket>/
aws s3 cp Salon.Api.zip        s3://<artifact-bucket>/
aws s3 cp Booking.Api.zip      s3://<artifact-bucket>/
aws s3 cp Notification.Api.zip s3://<artifact-bucket>/
aws s3 cp Gateway.zip          s3://<artifact-bucket>/
aws s3 cp Assistant.Api.zip    s3://<artifact-bucket>/
```

Find the default VPC and its subnets, and the exact .NET 10 platform name:

```bash
aws ec2 describe-vpcs --filters Name=isDefault,Values=true --query "Vpcs[0].VpcId" --output text
aws ec2 describe-subnets --filters Name=vpc-id,Values=<vpc-id> --query "Subnets[].SubnetId" --output text
aws elasticbeanstalk list-available-solution-stacks --query "SolutionStacks[?contains(@, '.NET 10')]"
```

Create the stack (about 20 minutes; RDS and ElastiCache are the slow parts):

```bash
aws cloudformation deploy \
  --template-file infra/salonbooking.yml \
  --stack-name salonbooking \
  --capabilities CAPABILITY_IAM \
  --parameter-overrides \
    VpcId=<vpc-id> \
    SubnetIds=<subnet-1>,<subnet-2>,<subnet-3> \
    ArtifactBucket=<artifact-bucket> \
    DbPassword=<letters-and-digits> \
    JwtSigningKey=<64 random characters> \
    AdminEmail=<email> AdminPassword=<password> \
    SendGridApiKey=<key> GeminiApiKey=<key> \
    GitHubToken=<token>
```

Start the first website build (later pushes to `main` build automatically):

```bash
aws amplify start-job --app-id <AmplifyAppId output> --branch-name main --job-type RELEASE
```

Print the addresses:

```bash
aws cloudformation describe-stacks --stack-name salonbooking --query "Stacks[0].Outputs" --output table
```

## Tear down

```bash
aws s3 rm s3://<ImagesBucketName output> --recursive
aws cloudformation delete-stack --stack-name salonbooking-cf
```

## Known limitations

- Secrets are stack parameters and end up as Beanstalk environment properties; production would use Secrets Manager.
- Every service is a single `t3.micro` instance and the database is single-AZ — a test setup, not a highly available one.
- Service-to-service addresses use the EC2 DNS name of each environment's Elastic IP (`ec2-1-2-3-4.<region>.compute.amazonaws.com`). The `<region>.compute` form is right for every region except `us-east-1`, which uses `compute-1`.
