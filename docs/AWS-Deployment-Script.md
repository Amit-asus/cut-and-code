# SalonBooking on AWS — first deployment demo (talk script)

A script to read out loud with `SalonBooking-AWS-Demo.pptx`. Section N here is
slide N there, and the same text is in each slide's speaker notes. Section 8 is
the live demo.

---

## 1. Opening

Today I'll show you my SalonBooking app running on AWS. AWS is Amazon's cloud.
Instead of running everything on my laptop, we rent ready-made services from
Amazon.

This is the first time I'm showing the deployment. I'll cover five things:
what the app is, where each part lives in AWS, how one file creates all of it,
a live demo, and what I learned along the way.

---

## 2. The app in one minute

The app has five small back-end parts and a website. Each part does one job.

Identity handles sign up and login. Salon handles the salons, their services
and their photos. Booking handles the bookings and the free time slots.
Notification sends an email when a booking is made. The Gateway is the front
door: every request comes in through it, and it passes the request to the
right part. The website is what the user sees in the browser.

On my laptop, a tool called Aspire starts all of this for me: the database,
the cache, the file storage and the message queue. Aspire also tells each part
how to find the others. But Aspire only does this on my laptop. In AWS, I had
to choose a real service for each piece.

---

## 3. The choices I made

**Elastic Beanstalk to run the five parts.** I give it a zip of my published
.NET app. It creates a server, installs .NET and runs the app. I don't have to
build containers or manage the servers myself. Each part gets its own small
server.

**Amplify for the website.** Amplify connects to my GitHub repository. When I
push to the main branch, it builds the React site and publishes it by itself.
So the website already deploys automatically.

**CloudFormation to create everything.** The first time, I set everything up
by hand in the AWS console, to learn each step. Then I deleted all of it and
wrote it down as one file. Now one command creates the whole setup, and one
command removes it.

---

## 4. Where each part lives

| On my laptop | In AWS | In simple words |
|---|---|---|
| The five app parts | Elastic Beanstalk, one server each | AWS runs my zip on a small server |
| Postgres database | RDS | A database that Amazon runs |
| Redis cache | ElastiCache (Valkey) | A fast memory store, run by Amazon |
| Salon photos | S3 | File storage |
| Message queue | SQS | A post box between Booking and Notification |
| Website | Amplify | Builds and hosts the website from GitHub |
| HTTPS for the API | CloudFront | Gives the Gateway a secure `https` address |
| Passwords and keys | Stack parameters | Typed in at deploy time, hidden on screen |

Everything runs in Sydney, the `ap-southeast-2` region.

---

## 5. One file creates everything: CloudFormation

CloudFormation is AWS's way of writing your setup as a file. You describe what
you want, and AWS creates it in the right order. The whole set is called a
stack. Mine is called `salonbooking-cf`, and the file is
`infra/salonbooking.yml`.

The file has four parts.

**Parameters.** These are the values I type in when I deploy: the database
password, the login signing key, the admin email and password, the SendGrid
and Gemini keys, and a GitHub token. Secret ones are marked `NoEcho`, so AWS
hides them on screen.

**Resources.** This is the list of things to create: three firewalls, the
database, the cache, the photo bucket, the queue, the permissions, the five
Beanstalk environments, CloudFront and the Amplify website.

**Links between resources.** The file connects them for me. For example, I
never type the database address anywhere. CloudFormation creates the database
first, then passes its address to the services that need it.

**Outputs.** When it finishes, it prints the website address, the API address
and a few others.

To deploy, I build the five zips, upload them to an S3 bucket, and run one
command: `aws cloudformation deploy`. It takes about twenty minutes. The
database and the cache are the slow parts.

---

## 6. Safety: only the Gateway is open

Only the front door should be reachable from the internet. I did this with
three firewalls. AWS calls a firewall a security group.

- The **Gateway** firewall lets in web traffic from the internet.
- The **back-end** firewall lets in traffic only from the Gateway and from the
  other back-end parts.
- The **data** firewall lets in database and cache traffic only from the
  back-end parts.

I tested this from outside. The Gateway answers. The other four parts do not
answer at all. The database is also marked as not public.

CloudFront sits in front of the Gateway, so the website can call the API over
`https`.

---

## 7. How a booking flows through AWS

Let me follow one booking before I show it live.

The user opens the website on Amplify. The website sends the booking to
CloudFront, which passes it to the Gateway. The Gateway passes it to Booking.

Booking checks that the slot is free in the database, saves the booking, clears
the cached time slots in ElastiCache, and drops a message in the SQS queue.
Then it replies to the user straight away.

Notification picks up the message. It asks Identity for the customer's email
and Salon for the salon and service names. It asks Gemini to write the email,
and sends it with SendGrid.

---

## 8. Live demo

*(Open `https://main.d3n7t0145o4skg.amplifyapp.com`.)*

1. **Log in as the admin.** This goes through CloudFront and the Gateway to
   Identity, which checks the password in the RDS database.
2. **Create a salon and upload a photo.** The photo goes to S3. Notice the
   image address on the page is an S3 address.
3. **Add a service to the salon.**
4. **Open the salon and pick a date.** The free time slots come from Booking.
   The second time I open the same date, they come from the ElastiCache cache.
5. **Book a slot.** The slot now shows as taken.
6. **Open the email inbox.** The confirmation email has arrived. It went
   through SQS, Notification, Gemini and SendGrid.
7. *(Optional)* **Show the AWS console:** the CloudFormation stack and its
   resources, and the five Beanstalk environments, all green.

*Backup if something fails live:* open the logs in CloudWatch, under
`/aws/elasticbeanstalk/<environment>/var/log/web.stdout.log`.

---

## 9. What I had to change in the code

The app was written for my laptop. Before it could run on AWS, I changed four
things.

1. **Storage and queue.** On my laptop, photos and messages use Azure test
   tools. I added S3 and SQS versions. A setting picks which one to use, so
   the app still runs the old way on my laptop.
2. **Website address.** The Gateway only accepted requests from the website on
   my laptop. The allowed address now comes from a setting.
3. **How the parts find each other.** On my laptop, Aspire gives each part the
   others' addresses. On AWS, those addresses now come from settings.
4. **The admin password.** It was written in a code file. On AWS, it is typed
   in at deploy time instead.

---

## 10. Problems I hit and what I learned

**Settings with a dash in the name disappear.** Aspire names the parts like
`identity-api`, with a dash. On Beanstalk's Linux servers, a setting with a
dash in its name is silently dropped. So the Gateway did not know where
Identity was, and every request failed. I fixed it in the shared code: the
setting is now written with an underscore, `identity_api`, and the code turns
it back into the name with a dash.

**Only five public addresses per region.** Each single-server environment uses
one fixed public address, and my account allows five. So I can't run two
copies of this setup side by side.

**A name that already exists stops the deploy.** CloudFormation checks names
before it starts. If an old app with the same name exists, it fails. So all
the names now come from the stack name.

**The cache needs encryption in the address.** ElastiCache encrypts traffic, so
the cache address has to end with `ssl=true`.

**Logs.** Downloading logs from Beanstalk failed with an access error. Instead,
the logs stream to CloudWatch, and I read them there.

---

## 11. What is still open

- **Secrets** are plain settings on the servers. Next: Secrets Manager.
- **One server per part and one database copy.** This is a test setup. It
  doesn't stay up if a server fails.
- **One internal route is reachable.** Identity has an internal route with no
  login check, meant only for other parts. The Gateway still passes it on, so
  I need to block it there.
- **Database updates at start-up.** Each part updates its own tables when it
  starts. With one server that's fine. With many servers, this should become a
  separate step.
- **Cost.** The setup costs about $5 a day, which is roughly $150 a month if it
  ran all the time. Most of that is the five servers, the database and the
  cache, all on the smallest sizes. Because it's one CloudFormation stack, I
  can delete it after testing and recreate it in about twenty minutes when I
  need it, so I only pay while it's in use.

---

## 12. Summary and next steps

To sum up: the whole app is running on AWS today, and one CloudFormation file
creates all of it. The five services run on Elastic Beanstalk. Amazon runs the
database, the cache, the storage and the queue for us. Only the Gateway is
open to the internet, and the website builds itself from GitHub on Amplify.
The whole setup costs about $5 a day, and I can remove it with one command
when it's not needed.

My next steps are small. First, I'll block the one internal route that still
passes through the Gateway. Then I'll move the secrets into Secrets Manager.
After that, I'll try running the services as containers on Fargate.

I'm happy to take questions.

---

## Likely questions

**Why Elastic Beanstalk and not containers, like Fargate or Kubernetes?**
For a first deployment it was the simplest. Beanstalk runs my published .NET
app directly, so I didn't need to build containers first. Containers on
Fargate are my next step. Kubernetes would be much more to learn and manage
for five small parts.

**Why CloudFormation and not just the console?**
The console is good for learning, but hard to repeat. With the file, I can
create the whole setup in one command, remove it in one command, and see every
change in Git.

**Why CloudFront in front of the Gateway?**
The website is served over `https`. A browser blocks an `https` page from
calling a plain `http` API. CloudFront gives the Gateway an `https` address
without me managing a certificate.

**Why not keep Azure for storage and the queue?**
It would work, but then we use two clouds, with two bills and two things to
manage.

**Why one database server and not three?**
Cost. Each part still has its own separate database on that server, and they
can be split later.

**Is the database open to the internet?**
No. It is marked as not public, and its firewall lets in only the back-end
parts.

**How do you update the app?**
For the website, I push to GitHub and Amplify rebuilds it. For the back end, I
upload new zips and run the deploy command again. The secrets are remembered,
so I don't type them again.

**How do you remove it all?**
Empty the photo bucket, then run `aws cloudformation delete-stack`. Everything
the stack created is removed.

**How much does it cost?**
About $5 a day, or roughly $150 a month if left running. Most of it is the
five servers, the database and the cache, all on the smallest sizes. Since the
stack can be deleted and recreated in about twenty minutes, I only pay while
it's in use. A production setup with more than one server per service and a
standby database would cost more. Running the services as containers on
Fargate could bring it down, because several services could share capacity.
