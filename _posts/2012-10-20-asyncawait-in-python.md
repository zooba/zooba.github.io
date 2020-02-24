---
layout: post
title: Async/await in Python
slug: asyncawait-in-python
category: Python
tags:
- async
---

Those who follow the development of C# and VB probably know about the recently added [Async and Await](https://msdn.microsoft.com/en-us/library/vstudio/hh191443.aspx) keywords, which turn code like this:

```
int AccessTheWeb() {
    int result;
    Thread thread = new Thread((() => {
        HttpClient client = new HttpClient();
        string urlContents = client.GetString("http://msdn.microsoft.com");
        result = urlContents.Length;
    }));
    thread.Start();
    DoIndependentWork();
    thread.Join();
    return result;
}
```

Into code like this:

```
async Task<int> AccessTheWebAsync()
{ 
    HttpClient client = new HttpClient();
    Task<string> getStringTask = client.GetStringAsync("http://msdn.microsoft.com");
    DoIndependentWork();
    string urlContents = await getStringTask;
    return urlContents.Length;
}
```

For the last week or two, an intense discussion has been occurring on the [python-ideas](http://mail.python.org/mailman/listinfo/python-ideas) mailing list about asynchronous APIs, to which I've been contributing/supporting an approach similar to async/await. Since I wrote a 5000 word essay this week on the topic, I'm going to call that my post.

Read: [Asynchronous API for Python](/blog/async-api-for-python).