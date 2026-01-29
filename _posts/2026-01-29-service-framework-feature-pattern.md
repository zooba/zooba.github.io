---
layout: post
title: API design for features in a service framework (part 1)
slug: service-framework-feature-pattern-1
category: Python
keywords:
- python
- pattern
- framework
- api design
---

I found myself providing design feedback to a project recently, and decided that the discussion was worth expanding beyond me ranting over a video call.

This project is a multi-service framework. Not an uncommon project, particularly in areas where there are many providers of *almost* equivalent services, and a lot of competition. Using a framework that supports many of them lets you - the developer - build your application once and then conveniently switch provider without a full rewrite.

Sounds great on paper, unless you've done it before, in which case it sounds like a blatant lie on paper. So let's look at how to think about one part of such a design, and how we can help make the promise come true.

<!--more-->

For the sake of keeping it simple, let's talk about hypothetical *storage services*. You know, where you might upload a file to save it, because you want to be able to access it later on. (I've got no real interest in talking about the actual providers, I just want to offer a concrete example so I don't have to have paragraph-long definitions of each of my terms.)

In this scenario, there are multiple providers (the companies our users pay) of storage services (the thing they want), and we (us) are writing the software framework to let other developers (our users) write code that does something interesting for *their* users.

Let's put that in terms of some code. Here's an example of what our users want to write:

```python
if config.provider == 1:
    client = Provider1StorageClient(config.credentials)
elif config.provider == 2:
    client = Provider2StorageClient(config.credentials)

# --- the configuration line ---

for f in client.list_files("*.txt"):
    print(f.name, "is a text file")
```

I've marked the "configuration line" in the code, which is the point where the code flow switches from "I'm doing all my provider-specific configuration" to "I'm doing provider-independent logic". You'll notice that there's nothing specific to either provider below the line.

**The framework promise:** When the user switches provider, they modify what's _before_ the configuration line, but shouldn't have to touch what comes after.

In terms of design, that implies that every `...StorageClient` class must have identical API. (You might be tempted to immediately reach for a subclass here, but it really doesn't matter, nor does it help.) Whatever code might do _below_ the configuration line must apply equally to all client classes - if you ever see a specific provider referred to again, that is a deficiency in your design!

To round out this simple example and illustrate the first type of issue you may hit, here are partial implementations of those two providers.

```python
class Provider1StorageClient:
    def list_files(self, pattern):
        from provider1 import get_file_list
        return [Provider1StorageFile(f)
                for f in get_file_list(self._connection, pattern)]


class Provider2StorageClient:
    def list_files(self, pattern):
        if pattern not in (None, "*", "*.*"):
            raise NotSupportedError("cannot filter files by pattern")

        from provider2 import list_all_files
        return [Provider2StorageFile(f)
                for f in list_all_files(self._connection)]
```

Let's look at three major points from this (two positive, one negative).

First, each provider's own library uses different names for these functions - `get_file_list` and `list_all_files` - so we've provided a useful service to our users by effectively renaming them both to `list_files`. _Our_ users have less to think about when they're coding now.

Second, each provider's result types are different, so we wrap them in provider-specific file classes which, you guessed it, also have identical API. So our users can just assume a `.name` attribute, without having to know whether the underlying value was `.filename`, `.metadata['name']` or something else.

Third, and this is the negative, provider 2 doesn't support filtering, so we raise an error.

Uh... yeah. That's not very nice.

What we have here is feature divergence between two services. They don't offer *exactly* the same functionality, which means they need to be used differently to get the same result. But the problem here is **passing it along to our user**, when the very reason they're using us in the first place is to cover over these differences!

So what should we do? Implement filtering ourselves? Remove filtering from all other providers' classes to make it "fair"? The answer, of course, varies. Perhaps the choice in this case is fairly obvious, but we'll get to a more complex example in the next post.

First, though, let's look at some things for you to consider when deciding how to approach this.


## Do we know what the functionality should be?

Sometimes there isn't a reliable definition of the functionality, making it hard to align providers or implement workarounds. If we're going to take multiple service's definitions of "pattern" and promote them to our user, we need to be able to define what that means. Especially if we're going to implement it ourselves for cases where the provider doesn't do it. And if we can't define it, we probably shouldn't be offering it.

But bear in mind that users calling `list_files()` are _below_ the configuration line. That means it should behave the same across all providers. A feature that is used above the configuration line can be provider-specific. However, our implementation of `list_files()` is entirely provider-specific, and so *we* should be taking full advantage of that. Our job as the framework authors is to know the providers' nuances thoroughly. In this example, that may mean we check the filter and decide whether to pass it along to the provider, or to process it ourselves, or to do some split of both. As long as we know what the functionality should be, we are in the best position to provide it.


## Is it worth using the provider's functionality?

Some features are convenient for users, but only for the sake of saving them writing code. If the provider's service doesn't handle it in some useful or efficient way, then it may not be worth offering. In this example, service-side filtering is usually much more efficient than client-side, so it's probably worth offering it where we can. But if we implement our own client-side filtering for providers that don't have it, our users get to write the same code for both.

The nuance on "identical API between providers" here is that the *observed* behaviour doesn't have to be identical. Otherwise, why would anyone ever switch provider? Our API definition for `list_files()` promises to give a filtered list of files, but the time taken and bandwidth used is not part of that definition. That kind of thing would ideally be noted in the documentation for the specific provider's client, to help our users make informed choices, but not part of the API spec.

This is actually critical for our own development _and our user's long-term happiness_. Because what happens when provider 2 implements filtering in their v2 API, and we've already "promised" our users that filtering will be client-side? Yep, that's right, making things better would break our promise, which inevitably causes the workarounds our users have built to get worse. This is why we keep the API promise purely functional ("returns a filtered list of files"): so that the implementation can silently choose to be more efficient.

Remember, the goal here is that our users shouldn't have to modify their code below the configuration line for the sake of the services. So when provider 2 adds a new API, we should be adopting it in the framework as soon as possible so our users can just take a fully API-compatible update of our framework to get the benefits.

## When should we fail?

Okay, these have been easy so far, let's discuss some questions that might start to expand your mind:

Inevitably, there will be times that we simply can't handle what the user has asked for. Maybe we (or a provider) can't support multiple `*` characters in the filter, or a certain provider has a limit on filename length. In other words, something that looks like a `ValueError` (the type of parameter is correct, but the value can't be used), as opposed to a `TypeError` (the code must be incorrect because variables are obviously getting mixed up).

The short answers are: "consistently" and "where the caller expects errors".

Error cases are going to require the user to write exception handling below the configuration line - that means it can't be provider-specific. A service that is permanently missing a feature is a great reason for them to never configure that provider again, but that's an above-the-line decision, not something to make users consider every single time they want to use a function.

An often unspoken design concept in Python (and arguably it fits in any language) is that any error the user should be prepared to be handled should come as a result of something they've explicitly invoked, such as a function call. The people who know too much but not enough are saying "but everything is a function call in Python" right now, to which I say "that's why I said *explicitly* invoked". Raising exceptions from things that look like assignments or attribute access is surprising, and should be avoided (allowance granted for cases where the user has explicitly instantiated a lazy object, such as remote/foreign function call wrappers).

This concept is fundamental to why each client should have identical API. Accessing an attribute on a "known" object (one that the user instantiated in their code) shouldn't ever raise a runtime error. If the user knows that the storage clients have a `list_files` method, then `client.list_files` should not be the way they find out that a particular provider doesn't support it. Why? Because attribute access isn't the user invoking something.

However, as soon as they _call_ it, that's where errors might occur. For a variety of reasons - network, credentials, timeouts, memory overflows, etc. Good, safe code is going to be handling exceptions around every `list_files()` call, regardless of which provider they're using, because it's the point where exceptions might occur. If you start giving them a `NotSupportedError` here, the handling is likely to be much the same as for any other error. (Of course the preference is still to provide a slow, client-side implementation, so that there's no error at all, but there are times that's not feasible.)

Now, some will suggest that the absence of the method entirely is a clearer indicator of a coding error - if your program _needs_ `list_files`, then you should find out as soon as possible if it's missing - and I agree that it is a clearer indicator, but disagree on it being the user's coding error. In part because it was *our* job to help the user avoid such an error, but mostly because it leads to ugly code.

It's very unlikely that users will be able to know ahead of time that the attribute will be missing. No matter how many type annotations you use, you won't be able to statically infer types throughout a complex application to know exactly which provider you'll be using at any point. The point of our framework is to let our users _avoid_ having to know which provider they're using! And since you can't do it statically, you will force your users into this code pattern:

```python
try:
    client.list_files
except AttributeError:
    # handle unsupported feature
else:
    try:
        client.list_files(...)
    except Exception:
        # handle all other errors
```

Trust me, nobody wants to write code like this. `AttributeError` is a bit annoying, because you should only ever handle it when the _only_ operation is an attribute access (as in the code above), and the rest of the time it should go unhandled (as it represents a coding error, just like `TypeError`). Having to do a test before doing a call _where you're already having to handle errors_ completely disrupts the flow of the code, making it very hard to read and reason about. Consider the alternative, where the function exists but raises:

```python
try:
    client.list_files(...)
except NotSupportedError:
    # handle unsupported feature
except Exception:
    # handle all other errors
```

Your users will appreciate not having to guess where errors might occur. The more you can bundle them together to always occur as the result of an explicit function call, the better.

## Can our users work around us?

What happens when one of our users needs something that we don't offer? Before you try too hard to apply this question to filename filtering, the answer there is pretty simple - they can pass an empty filter and do the filtering themselves.

This idea speaks to the need to provide *building blocks* and *passthroughs* as well as the main helpful API we hope our users will focus on.

A building block is a lower-level version of functionality, still abstracted across the different providers, but implemented in a way that puts more work onto our user. For example, our `list_files()` may return a single list, but some services might support paging (chunked results). So we add `list_files_by_page()` that returns an iterable of lists of filtered files. Services without paging support can implement this by using their `list_files()` result and returning it on its own (one single page), while services with paging may implement the paged one directly and make their `list_files()` be based on it. Or a single function that sends and receives "raw", unprocessed responses (for example, `dict`s parsed to/from JSON, rather than rich objects).

It's very likely that this is how you're developing the implementation anyway, but by making it a carefully designed part of your public API, you let your users choose which approach they want. Users who desperately need paging have the choice to build their code around that API, and if they later switch to a provider that doesn't support it, your implementation can cover the difference and their code doesn't have to change. (Someone sending raw requests will likely get errors direct from the provider and *will* have to change, but only in the places where they used it.)

What's important here is to consider what happens if you *don't* have this building block available. For some users, they'll simply acknowledge the limitation, but some will absolutely need the functionality. If you haven't offered them a way to get it through your generic API, they'll have to bypass your framework to access the service directly. At that point, the value of your framework very rapidly falls to zero, and it makes more sense for the user to leave you and commit to a provider directly. In other words, you've failed your user by making them leave.

A passthrough is similar. Quite often, despite us pretending that our users don't know which provider they're on, they actually do know. Using our framework is preparing to lessen the workload in case it changes, but day-to-day they know exactly who the provider is and what functionality they have available. And with online services, that functionality can also change day-to-day.

However, when you've limited access to providers through your consistent API, now your users can't access any of the new features until you've implemented them. Perhaps you're staffed well enough to add and release new features on a daily basis, but it's probably not a situation you want to find yourself.

The passthrough is how you let your users bypass our data models and validation, so that *they* can opt into features you haven't exposed (yet), rather than forcing them to leave your framework. A passthrough may look like an option to receive a raw response - for example, skipping the `ProviderNStorageFile` types from earlier - or to pass through a parameter to the service unprocessed - for example, using a filter pattern that is supported by a specific provider but blocked by our own validation.

One reasonable form of passthrough for many contemporary service providers is to accept arguments as either the "right" type, or as a `dict`. Commonly, arguments are not dicts, but the serialisation format is going to look like one (such as JSON or a known struct layout). When an argument is passed that is already in the serialisation format, your API can simply pass it along, trusting the user to know what they're doing. To be even more explicit, you could even have a basic wrapper type that can contain any value, and will be detected by your client implementations and used to pass the value through without processing.

```python
def list_files(self, pattern):
    if isinstance(pattern, Passthrough):
        actual_pattern = pattern.value
    elif isinstance(pattern, dict):
        # Not even checking keys! We trust the user completely
        actual_pattern = pattern
    else:
        actual_pattern = {"pattern": self._validate_pattern(pattern)}
    ...
```

Some may be concerned that this is an exploitable security risk, but it's really not, at least not in *our* code. The type of value passed to a function is fully under the control of the code that's calling it, and so the user has to have chosen this in their code, making it *their* risk. And the harder you try to "protect" users from that "risk", the more likely you are to push them away from using your framework entirely, which only raises the risk even further! Knowing when to let your users make their own mistakes is a critical part of designing a framework.

## Intermission

Let's take an intermission. I have more to discuss, and a more complex example to bring, involving optional features and some more moderate passthrough options, but that can wait for the next post.

Feel free to link and discuss anywhere you like. The only place I'll respond to comments or participate in discussion is on X ([@zooba](https://x.com/zooba)).
