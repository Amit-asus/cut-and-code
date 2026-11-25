# AWS console walkthrough (short script)

About 5–7 minutes. Read the quoted lines out loud while you click.

**Before you start**
- Log in as the IAM user, not the root account.
- Switch the region (top right) to **Asia Pacific (Sydney) ap-southeast-2**. If you don't, every list will look empty.
- **Don't open Configuration → Environment properties** on the Identity or Notification environment while sharing your screen. The passwords and API keys show there in plain text.

---

## 1. CloudFormation — the stack

*Search "CloudFormation" → Stacks → `salonbooking-cf`.*

> "This is the stack. Everything you'll see in the next few minutes was created by this one stack, from one file in my repository."

*Open the **Resources** tab.*

> "This is the list of everything it created: the firewalls, the database, the cache, the photo bucket, the queue, the five servers, CloudFront and the website. If I delete the stack, all of this goes away."

*Open the **Parameters** tab.*

> "These are the values I typed in at deploy time. The secret ones show as stars, so they're hidden."

*Open the **Outputs** tab.*

> "And these are the addresses it printed at the end: the website and the API."

---

## 2. Elastic Beanstalk — the five services

*Search "Elastic Beanstalk" → Environments.*

> "Here are my five back-end services, each in its own environment: identity, salon, booking, notification and gateway. Each one is a small server running my .NET app. They're all green, so all five are healthy."

*Click **salonbooking-cf-gateway**.*

> "This is the Gateway, the front door. Elastic Beanstalk installed .NET for me and runs the zip I uploaded. It's the only one of the five that's open to the internet."

*Point at **Running version** on the page.*

> "This is the version of the code it's running. To update the app, I upload a new zip and deploy again."

---

## 3. RDS — the database

*Search "RDS" → Databases → click the Postgres instance.*

> "This is the Postgres database. Amazon runs it for me. Each service still has its own separate database inside it: identity, salon and booking."

*Point at **Publicly accessible: No**.*

> "It's not public. Only my back-end services can reach it."

---

## 4. ElastiCache — the cache

*Search "ElastiCache" → Valkey caches (or Redis OSS caches).*

> "This is the cache that Booking uses for the free time slots. The first time someone looks at a day, Booking works out the slots from the database. After that, they come from here, which is faster."

---

## 5. S3 — the photos

*Search "S3" → the bucket whose name starts with `salonbooking-cf-imagesbucket` → open it.*

> "These are the salon photos. When I upload a photo on the website, it lands here."

*(After the demo you can refresh and show the new photo.)*

---

## 6. SQS — the queue

*Search "SQS" → `salonbooking-cf-booking-created`.*

> "This is the post box between Booking and Notification. When a booking is made, Booking drops a message here. Notification picks it up and sends the email. Messages available is zero because Notification has already picked them all up."

---

## 7. EC2 security groups — the firewalls

*Search "EC2" → Security Groups → filter `salonbooking-cf`.*

> "These are the three firewalls. The Gateway's lets in web traffic from anyone. The back-end one lets in traffic only from the Gateway. The data one lets in only the back-end services. That's how only the front door is open."

*(Optional: open the back-end one → Inbound rules, to show the source is another security group, not 0.0.0.0/0.)*

---

## 8. CloudFront — https for the API

*Search "CloudFront" → Distributions.*

> "This gives the Gateway a secure https address. The website calls the API through here."

---

## 9. Amplify — the website

*Search "Amplify" → `salonbooking-cf-web` → main branch.*

> "This is the website. Amplify is connected to my GitHub repository. Every time I push to main, it builds the React site and publishes it. Here you can see the build history."

*Click the domain link to open the live site. This leads straight into the demo.*

---

## 10. CloudWatch — the logs (only if needed)

*Search "CloudWatch" → Log groups → filter `salonbooking-cf-gateway` → `web.stdout.log`.*

> "If something goes wrong, the logs from every service end up here."

---

**Close:**

> "So that's the whole setup: five services on Elastic Beanstalk, Amazon runs the database, cache, storage and queue, Amplify hosts the website, and one CloudFormation stack ties it all together."
