---
layout: page
title:
slug: async-api-for-python
redirect_from: /blog/async-api-for-python
---

So over on the [python-ideas](https://mail.python.org/mailman/listinfo/python-ideas) mailing list for the last couple of weeks, a discussion has been raging about adding a standard asynchronous API to Python. Quite coincidentally, we spent a couple of weeks at work looking into exactly this, so we were keen to join the discussion.

What I intend to do here is outline the problem as I see it (based on my own objectives, experience and various discussions from the mailing list) and propose a portable and extensible API to solve it.

# The Problem
At its simplest, the problem is that computer programs are [I/O-bound or CPU-bound](https://stackoverflow.com/a/868577). These days, I/O-bound programs are common because CPUs have reached such amazing speeds that electrons travelling longer distances don't quite keep up. I/O is also inherently less reliable, and so more complicated protocols are involved to ensure that the results are correct, at the expense of time.

Many applications remain CPU-bound, and most significant programs will have components that are I/O-bound and others that are CPU-bound. Synchronous APIs do not allow any overlap, so while a program is waiting for the response from a device, it is not using the CPU. Likewise, while the CPU is busy with some sort of loop, it is not able to perform any I/O.

Luckily, pure synchronous APIs have not been seen in a while. Most operating systems, and certainly all of those for which Python may be used, have varying ways of starting an operation and doing something else while it completes. These are called non-blocking APIs, since the call will return without waiting for a result, and typically require callbacks or polling to get the response.

## Callbacks are good
When a non-blocking API accepts a callback, this removes the responsibility of checking the result from the developer. Using a long car ride as an analogy, a callback is the equivalent of saying "I'm going to have a nap; when we arrive, somebody wake me up." It is not important _who_ does the waking up, provided that being woken up is expected. The sleeping person has every right to be upset if they are woken up early, or if they are woken in an inappropriate way.

However, it is likely that a callback will not arrive the instant that the operation completes, such as when the car driver unloads the bags before waking the sleeper. Also, the callback may not come at a suitable time for the sleeper (in the middle of a good dream, perhaps), which could also be problematic. (In practice, general-purpose operating systems do not allow this to occur.) There may also be limited guarantees as to where a callback is executed with respect to threads, program state and locks, restricting the amount of work that can safely be performed in a callback.

Callbacks are a well-defined contract between the application and the system managing asynchronous operations, easy for the developer to implement, but poorly defined with respect to timeliness and coherent program state.

## Polling is also good
In many cases, non-blocking APIs prefer polling, making it the developer's responsibility to check whether the operation has completed. Continuing the above analogy: "Are we there yet? Are we there yet? Are we there yet? Are we there yet?"

Assuming that the operating system does not tell the asker to stop asking (which does actually happen in some rate-limited scenarios), eventually the response will be provided. Because this is a reply and not an interruption, it can be guaranteed to appear at a suitable location within the code, outside of any locks that may interfere with it.

The downside of polling is that when the "Are we there yet?" asker has to take a deep breath, they are not going to find out whether they have arrived until they ask again. If they become distracted by some other task, they may forget or not have the opportunity to ask for a while. In enclosed systems it is possible to interleave a long operation with unrelated polling, but this is rarely composable in a way that suits a standard API.

Polling is also a well-defined contract, ensures program state is safe, allows the developer to prioritise asking versus doing other work, but is difficult to compose without full cooperation between all developers and libraries.

# Why (CPU) Threads Are Not The Solution
An easy way to perform two tasks at the same time is to simply use blocking operations and CPU threads. Now, even on a single CPU system, one thread can be I/O-bound and the other CPU-bound and the pre-emptive scheduler (which is not the only kind, but it is the most common in general-purpose OSs) will minimise the latency.

This is simple, but also limited. Scale this up to 100 threads and it is likely that resource contention will negate any benefit. Aim for one thread for each connection on a busy server and you're in trouble. Using a pool of threads reduces the impact of creation and destruction, but may still incur significant context-switching issues or locking issues.

Long-running, CPU-bound tasks are often best served by a CPU thread. The blocking operation in this case could be any form of processing where the result is not required immediately, which is true for any case involving a freely-interactive user interface (for example, a GUI, but not normally a TTY). The aim here is to perform the long operation while the user is not interacting with the program, but suspend it when the user wants to do something.

# Why Futures ARE The Solution
Futures, as currently implemented in [concurrent.futures](https://docs.python.org/3/library/concurrent.futures.html#future-objects) are perfectly suited to a standard API because they are an interface, not an implementation.<sup>1</sup> The nature of non-blocking calls requires that _something_ is returned, and that the caller either provides a callback or polls for a result. The `Future` class encapsulates both of these options, allowing the caller to choose the contract they prefer. Further, because a `Future` is independent of the operation, the callee can choose their own implementation.

(<sup>1</sup> Obviously there is an implementation there, but users of futures should not be depending on implementation details. This is why functions like `set_result` are documented as "private".)

For example, consider reading a buffer from a file. The Python API could be "read_async(byte_count : int) -> Future". To implement this on Windows you could call [ReadFileEx](https://msdn.microsoft.com/en-us/library/windows/desktop/aa365468.aspx) and provide a "completion routine" (callback) that will set the future's result. On Linux, [read](https://www.kernel.org/doc/man-pages/online/pages/man2/read.2.html) will<sup>2</sup> always block while reading file data into memory, and the only truly non-blocking way to read is to use a separate thread (more on this idea later). Because the interface is through a `Future`, all code calling "read_async(x).result()" or "read_async(x).add_done_callback(...)" is portable _and_ efficient for both platforms.

(<sup>2</sup> The man-page suggests that while some file systems may respect `O_NONBLOCK`, most treat local files as always ready and then block while the read occurs.)

Requiring both polling and callback features of `Future` be implemented is not unreasonable, since callback APIs can be trivially wrapped with a polling interface, while polling and blocking APIs can become callback-based with the addition of a thread (more on this later). Importantly, it is necessary for the portability of the API that _all_ implementers support both `result()` and `add_done_callback()`. This does not require the standard `Future` class to be used directly - a subclass would be equally valid, and in some cases even beneficial (more on this later, too).

## Why "yield from" is irrelevant
So far absent from this discussion is any mention of `yield`, `yield from` and generators. Most suggestions involving `yield from` are using it as a mechanism to pass a future-like object all the way up the call stack to a scheduler.

This is precisely one of the approaches that I implemented before deciding that it was not a good solution. Basically, it works like this:

```python
def do_downloads():
    img_future_1 = download_image_async(image_uri_1)
    img_future_2 = download_image_async(image_uri_2)
    img_1 = yield from img_future_1
    img_2 = yield from img_future_2

def main():
    yield from do_downloads()

run_inside_scheduler(main)
```

The `download_image_async` function returns a 'task' object. At this stage, no images are being downloaded, what we have are effectively future promises: an object representing a task that will have some result in the future. Each of these are sequences of other tasks, as is the `do_downloads` function and `main`. The `run_inside_scheduler` method then iterates over the tasks, waiting for each to complete before it invokes the next. If we replaced `run_inside_scheduler` with `list`, the result might look like this:

```python
[
    # originally yielded from download_image_async(image_uri_1)
    <task open_url="">,
    <task read_url="">,
    <task parse_image="">,
    # originally yielded from download_image_async(image_uri_2)
    <task open_url="">,
    <task read_url="">,
    <task parse_image="">
]
```

As the scheduler receives each task object it will start it and "switch out" the task for another. Because `yield from` results in a flat sequence, this approach requires some form of fork command, whether that involves yielding tuples (in fact, a sequence of tuples of sequences of tasks) or some function returning the equivalent. Without forking, the scheduler has no other operation to perform while blocking and nothing is gained. In the example given, the scheduler has no choice but to block, since the result of the first task has to be sent in before the second task is obtained.

The problems with this approach largely come down to it not being composable and not particularly easy to get right. Using `yield from` is brittle because every frame must use it, requiring that every function be return an iterable. The top (most recent) _N_ frames don't need to be generators, but those cannot call into asynchronous functions under this model. As soon as one frame stops yielding tasks, any deeper frames cannot access the scheduler. This effectively denies library developers the freedom to use the new API, since that then forces their users into a particular model.

For this approach, an API defining the interface and semantics of the task objects would allow schedulers and tasks from different sources to work together. From the user's point of view, the API is that they must use `yield from` everywhere. No new objects or types are necessarily visible, which could be seen as a positive, but there is a lot of magic involved that could put off some users.

Another `yield from`-based approach uses yields solely as a way of interrupting execution, and not for passing information around. These approaches use a separate call before the yield to 'suspend' the current iterable and a call from another part of the program to result it. What these approaches are implementing is cooperative multitasking, which solves the problem of "I have one thread and need time-slicing", but do not lend themselves towards dealing with blocking operations. They also lack composability, not least because the risk of deadlocking any code with non-reentrant synchronisation is near certain unless all parts of the code are designed together. Further, the number of primitives required for such an API to be useful and portable is significant.

# Futures and "yield"
Amongst the discussion of using `yield from` for passing task objects up the stack, there have been some suggestions that `yield` is sufficient. This is another approach that has been implemented and, while potentially less efficient for a naive implementation, it has the benefit of being easier to understand and use.

The two parts of this approach are a function decorator `@async` and the `yield` expression, (these are very similar to the [Async and Await](https://msdn.microsoft.com/en-us/library/vstudio/hh191443.aspx) keywords in C# and VB), and the basic rules are:

1. All `@async` functions return a `Future`.
2. Yielding a `Future` within an `@async` function will defer the rest of the function until that `Future` completes.

This API does not specify anything about how or where asynchronous operations are run, as long as a future is returned. The most basic implementation runs everything synchronously:

```python
def async(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        final_future = Future()
        it = iter(fn(*args, **kwargs))
        try:
            fut = next(it)
            while True:
                fut = it.send(fut.result())
        except StopIteration as si:
            final_future.set_result(si.value)
        except BaseException as be:
            final_future.set_exception(be)
        return final_future

    return wrapper
```

(For brevity we assume that all values of `fn` are generators. It is easy enough to check and return a completed `Future` if not.)

The example from above could then be implemented as:

```python
@async
def do_downloads():
    img_future_1 = download_image_async(image_uri_1)
    # img_future_1 has started downloading already
    img_future_2 = download_image_async(image_uri_2)
    # now both images are downloading
    img_1 = yield img_future_1
    # when we reach here, img_1 is ready
    img_2 = yield img_future_2
    # if img_2 finished while we waited for img_1, this last
    # yield will be very fast.

def main():
    do_downloads().result()
```

What makes this approach more robust than using `yield from` is that we never actually pass an iterable anywhere. At any point in the callstack you can call `result()` on an `@async` function and wait for it, or you can obtain a `Future` from any source and `yield` it. Importantly, this code works today with Python 3.3 and an unmodified standard library.

For slightly better asynchrony, we can perform a slight refactoring to move the `send()` call into a callback:

```python
def async(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        final_future = Future()
        it = iter(fn(*args, **kwargs))

        def callback(prev_future):
            try:
                next_future = it.send(prev_future.result())
            except StopIteration as si:
                final_future.set_result(si.value)
            except BaseException as be:
                final_future.set_exception(be)
            else:
                next_future.add_done_callback(callback)

        first_future = next(it)
        first_future.add_done_callback(callback)

        return final_future

    return wrapper
```

(Again, assuming that `fn` is a generator yielding at least one future. This can also be handled, but the code is not as easy to read.)

Here we do not block in the `result()` calls, because they all occur within the callback, and `final_future` will be returned quickly. Any `@async` function that does not yield (or is not a generator) runs synchronously and `final_future` is returned already completed. It is the responsibility of the `@async` function to either invoke other `@async` methods or to obtain a `Future` some other way (such as using an [Executor](https://docs.python.org/3/library/concurrent.futures.html#executor-objects)) if it wants to avoid blocking.

So, after a lot of background material, this is the fundamental piece of a portable and extensible API. Its problem, and I don't deny that it is hugely significant, is a lack of efficiency compared to what OS-level APIs can provide. However, this can be resolved largely internally without forcing users to deal with anything other than `@async` and `yield` (and optionally `Future`). The next two sections discuss how this can be achieved (spoiler: both use an event loop).

## Solving Callbacks
The main problem with callbacks is a lack of control over when and where they are executed. Mitigating this normally involves doing nothing more than queuing some flag and returning, and allowing the main program to pick up the response at a convenient time. This is easily performed with an event loop that allows callbacks to directly access its queue:

```python
def event_loop(start_with):
    initialise_queue()
    start_with()
    while do_not_exit:
        workitem = queue.get_item()  # blocking call
        workitem()

def a_callback():
    queue.enqueue(lambda: ...)
```

Thread-safety has been omitted from that example, but is certainly necessary. In general, callbacks triggered from other CPU threads will run on those threads, so an event loop provides a way to bring execution back to the main thread. Otherwise, we may inadvertently transfer our main program onto a threadpool thread, risking throttling or incorrect priority settings.

This sample requires that every workitem queues more items, though it does allow for an empty queue to exist while tasks are running. It does not require items to be `@async` methods, and it actually works best if they are not. However, the queue should not ever need to be accessed from user code: `@async` will use it (by default) to schedule each step of its wrapped function.

One potential benefit of making the queue accessible is in GUI programming where controls can only be accessed from a particular thread. When a long running task is on a background thread, progress updates must be posted to the thread with the event loop. Importantly, this 'feature' does not require any special support, provided the UI's loop has some way to post a callable to it. A later example shows how this can be easily implemented on top of Tk.

## Solving Polling
The problem of polling, that we need to explicitly ask whether a task is complete, can also be solved with an event loop. Most polling APIs support both blocking and non-blocking calls to check the state. Integrating a select()-like call into the event loop allows both callback- and polling-style APIs to compose. An event loop in this case may look like this:

```python
def event_loop(start_with):
    initialise_queue()
    initialise_wait_dict()
    start_with()
    while do_not_exit:
        if wait_dict:
            workitem = queue.try_get_item()  # non-blocking call
            if workitem:
                workitem()
            else:
                ready_items = get_ready_items(wait_dict.items, queue)  # blocking call
                for wait_item in ready_items:
                    if item_id, item_callback is not queue:
                        queue.enqueue(item_callback)
                        del wait_dict[item_id]
        else:
            workitem = queue.get_item()  # blocking call
            workitem()

def when_ready(waitable_object, callback):
    wait_dict[waitable_object] = callback
```

(A complete, working example is below. This one is only meant to illustrate the concept.)

With this event loop, callbacks can be queued to execute inside the loop as before, and "waitable" (pollable) objects can also be added with a callback function. When no callbacks are queued, the loop will wait for one of the waitable objects to become ready. Importantly, this wait also includes the queue, since a callback from another source may enqueue another task. Any objects that become ready have their callbacks queued. If there is no pending I/O and no tasks queued the loop will block on the queue - if new I/O items might be added from outside the loop then we can also force the loop awake from `when_ready`.

Unfortunately, this approach lacks portability. Waitable objects can only be used with a particular event loop if the loop knows how to wait for them. An API can either define the maximum set of waitable objects, which is inherently restrictive, define the interface of waitable objects, which already exists (`Future`) and so prevents optimisation by special-casing, or defines a query interface.

In my opinion, the query interface is most appropriate, since it allows any event loop implementation to refuse an object it does not know how to handle. This forces the creator of that object to handle it in an alternative manner, which is likely to be more reliable than expecting the implementer of the event loop to handle arbitrary objects. Network-centric event loop implementations can handle socket objects in an efficient manner, while GUI event loops can refuse and force the caller to fall back on (presumably) a worker thread - either way, `read_url_async` continues to work for the user, which is the important part.

## Exposing the <strike>Event Loop</strike>Context
(For the remainder of this essay, I will refer to the event loop concept as a _context_, since it is more accurate in the way it is used. For example, code called from the event loop is in the _current context_, and queuing a callable to that loop will execute it in _that context_. The fact that an event loop exists is an implementation detail, and has no impact on users or library functions that make use of it.)

There are three places where code needs to be able to access the current context. The first is in `@async`, which has to schedule continuations using whichever context is active when the function is called. The second is any asynchronous function that wants to optimise for a particular object type. The third is user code that needs to execute code within the context but cannot use `@async` (most likely to occur with thread-sensitive GUIs). Further, to support these cases the context must be able to be captured, allowing it to be assigned to a variable that is then stored in a closure (this is trivially implemented with thread local storage, but is also relevant for the later specification).

Further, there must be a way to set the context, assuming that none already exists. This is to allow libraries providing "run"-like methods to set the context to themselves when they start. It may be (ab)used to nest different contexts, though that is unlikely to ever be a good idea, but since a "universal" context will always be inappropriate for some uses, there must be a way to change it.

Fortunately, this issue is easily solved in Python by providing a module with "set current context" and "get current context" functions. The interface below calls these `CallableContext.get_current()` and `CallableContext.set_current()`, being on the `CallableContext` class. (A simple static variable could be used, but functions allow some validation to be included.)

## Setting Callback Options
One limitation of this approach is a lack of available options. In many cases, the developer knows some information about how the operations will work that could be used to override the default behaviour. In order to expose a general mechanism for adding options, the best approach for this approach is to modify or wrap the `Future` objects. Consider the following hypothetical example:

```python
@async
def load_image_async(uri):
    conn = yield open_connection_async(uri)
    data = yield read_data_async(conn)
    img = yield filter_image_async(data)
    return img
```

For simplicity, we'll assume that the three `*_async` functions simply execute the operation synchronously on a separate thread. Breaking up the flow looks like this:

```python
@async

# create a `final_future` to return

# call the wrapped function
def load_image_async(uri):
    # call open_connection_async(uri)
    #   create a future
    #   create a thread that calls `open_connection` and sets the future's result
    #   return the future
    # yield the future

# add `self` as the done handler for the yielded future

#  (... do other things ...)
    #   thread completes, future.set_result(conn)

# callback is triggered on worker thread

# queue inner callback in the original context

# get future.result() and `send` it in
    # assign result to `conn`
    conn = yield open_connection_async(uri)
    
    # identical process
    data = yield read_data_async(conn)

    # identical process
    img = yield filter_image_async(data)

    return img
    # raise StopIteration(img)

# catch StopIteration

# call final_future.set_result(img)
```

What happens here is that control returns to the original context between each `yield`. In a case like this, there is little value and it would be beneficial to perform the three tasks on the same worker thread. (Beneficial, but not necessary. Doing this might break some programs, but not doing it should never be worse than a slight performance penalty.)

For convenience and extensibility, wrapping the future goes through a function `with_options`. (In the example implementation below, this does some monkey patching, but it could also create a proper wrapper instance.) By specifying the option `callback_context=None`, we specify that we want the callback (in this case, `__next__`) to be executed anywhere - in practice, this will be the same thread that `set_result` is called on. Because we know the rest of the generator will execute on a worker thread, we can use blocking APIs, and since the `return` becomes a `StopIteration` which becomes a `set_result()` call, any callers of `load_image_async` are unaffected.

```python
@async
def load_image_async(uri):
    conn = yield with_options(open_connection_async(uri), callback_context=None)
    data = read_data(conn)
    img = filter_image(data)
    return img                  # this still turns into a set_result(), so our callers are unaffected
```

Of course, since this relies on context implementation details it really is advanced functionality. If, for example, `open_connection_async` does not spawn a new thread, the remainder of the function will block the caller when the original implementation would not. Nonetheless, the ability to associate these kinds of options is necessary, but it is not necessary to use them to obtain correct functionality.

(One option may be something like `error_result`, which, if specified, sets the result to the provided value if an exception occurred and then does not `raise` inside `result()`. I am sure there are others that I have not considered.)

<a id="thecode"></a>
# Putting It All Together
Finally, we come to the code. I have split this up into three parts: the first is the signatures and brief documentation for the public API, the second has a sample implementation that works with Python 3.3 and the current standard library, and the third is some example code that works with the sample implementation.

All of this code can be downloaded from here: [PythonAsync.zip](/assets/PythonAsync.zip) (7kb)

## The Interface
```python
class CallableContext:
    '''Represents a context, such as a thread or process, in which callables
    may be executed. A callable context differs from a thread or process pool
    in that execution always occurs within the context regardless of the source
    of the callable.'''
    
    ## Static Methods
    
    get_current() -> CallableContext or subclass
    '''Returns the current context. Never returns None, but may return a very
    naive context if none has been set.'''

    set_current(context) -> CallableContext or subclass, None
    '''Returns the previous context, which should be restored when the new
    context becomes invalid.'''

    ## Instance Methods
    submit(self, callable, *args, **kwargs) -> None
    '''Adds a callable to invoke within a context. This method returns
    immediately and any exceptions will be raised in the target context.'''
    
    get_future_for(self, obj) -> Future, None
    '''Returns a `Future` that will be updated when `obj` is ready or is
    cancelled. The value of the returned Future's ``result()`` is a reference
    to `obj`.'''

def async(function) -> (`function` -> Future)
    '''Returns an async function for `function`.
    
    An async function always returns a `Future`, which must be used to obtain
    the value returned from the wrapped function.
    
    If the wrapped function is a generator, it must only yield instances of
    `Future`. The next step of the generator will occur after the yielded
    `Future` completes.
    
    If the wrapped function is not a generator, the returned `Future` already
    has the result and will not block when ``result()`` is called.'''

def with_options(future, [callback_context], [always_raise]) -> Future
    '''Adds options to a `Future`.
    
    These may be used with `async` to adjust the treatment of `future`. Calling
    this function with default parameters is a no-operation and `future` is
    returned unmodified.
    
    The current options are:
      continuation_context
        Specifies the `CallableContext` where the done callback will be
        executed. If unspecified, the current context is used. Specifying
        ``None`` will execute the callback in any context. When yielding this
        future from an `async` function, this affects where the statements
        following the yield will be executed.

      always_raise [bool]
        Specifies whether the `Future` should always raise its exception,
        regardless of the context it is set in. This will occur regardless of
        other calls to ``result()`` or ``exception()``.
    '''
```

## Sample Implementation
Documentation comments have been redacted; the code in the zip file contains the full comments. (It's only average documentation anyway...) It should also be noted that the default `CallableContext` implementation is _not supposed_ to do anything other than provide somewhere to put `get_current` and `set_current`. It does not need to be used as a base class for other contexts, though it is suitable for it.

```python
import collections
import concurrent.futures
import functools
import types
import threading

_tls = threading.local()

class CallableContext:
    '''Represents a context, such as a thread or process, in which callables
    may be executed. An awaiter context differs from a thread or process pool
    in that execution always occurs within the context regardless of the
    source of the callable.'''

    def get_current():
        '''Returns the current callable context.'''
        try:
            context = _tls.current_context
        except AttributeError:
            _tls.current_context = context = CallableContext()
        
        return context

    @staticmethod
    def set_current(context):
        '''Sets the current context and returns the previous one.'''
        try:
            old_context = _tls.current_context
        except AttributeError:
            old_context = None
        _tls.current_context = context
        return old_context

    def submit(self, callable, *args, **kwargs):
        '''Adds a callable to invoke within a context.'''
        callable(*args, **kwargs)   # The default implementation executes the 
                                    # callable synchronously.

    def get_future_for(self, obj):
        '''Returns a `Future` that will be updated when `obj` is ready or is
        canceled. The value of the `Future`'s ``result()`` is a reference to
        `obj`.
        '''
        return None                 # The default implementation cannot handle
                                    # any waitable objects.

class _Awaiter:
    '''Implements the callback behavior of functions wrapped with `async`.'''
    def __init__(self, generator, final_future):
        self.generator = generator                  # This generator contains "user" code.
        self.final_future = final_future            # This was returned to the caller.
        self.target_context = CallableContext.get_current() # Unless told otherwise, we will keep
                                                            # running the generator's code in this
                                                            # context.
        if self.final_future.set_running_or_notify_cancel():
            self._step(None)            # Async operations start "hot" - the first step has already
                                        # run. This lets us complete immediately if nothing is
                                        # yielded.
    
    def __call__(self, prev_future):
        if getattr(prev_future, '_callback_context_patched', False):
            # If with_options() was used (see below), we are already in the correct context.
            return self._step(prev_future)

        self.target_context.submit(self._step, prev_future) # Invoke the next step in the 
                                                            # original context.
        
    def _step(self, prev_future):
        result, ex = None, None
        if prev_future:
            ex = prev_future.exception()        # Get the exception without raising it.
            if not ex:
                result = prev_future.result()   # Only get the result if there was no error.

        try:
            if ex:
                next_future = self.generator.throw(ex)  # Pass errors back into the generator
                                                        #so they can be handled.
            else:
                next_future = self.generator.send(result)   # Pass results back in.
            
            next_future.add_done_callback(self)         # We only reach here if something was
                                                        # yielded, so assume that it was a Future.
        except StopIteration as si:
            self.final_future.set_result(si.value)      # Complete the future we returned originally.
        except BaseException as ex:
            self.final_future.set_exception(ex)         # Exceptions within __next__ have to
                                                        # propagate through this Future, since
                                                        # there is nowhere to pass them back in.


def async(fn):
    '''Decorator to wrap a generator as an asynchronous function returning a
    `concurrent.futures.Future` object.
    '''
    @functools.wraps(fn)
    def wrapper(*args, **kwargs):
        result = fn(*args, **kwargs)
        final_future = concurrent.futures.Future()
        if isinstance(result, types.GeneratorType):
            _Awaiter(result, final_future)  # Initialising _Awaiter is sufficient to start the 
                                            # operation running and it will notify final_future 
                                            # when it is complete.
        else:
            final_future.set_result(fn(*args, **kwargs))    # Non-generator methods still return
                                                            # a Future for consistency.
        return final_future
    return wrapper

def _alternate_invoke_callbacks(self, context):
    for callback in self._done_callbacks:
        try:
            if context:
                context.submit(callback, self)
            else:
                callback(self)
        except Exception:
            from concurrent.futures._base import LOGGER
            LOGGER.exception('exception calling callback for %r', self)


def with_options(future, **options):
    # Just monkey patching the Future. There are no doubt better ways to
    # achieve the same result, but without modifying Future's definition this
    # is easiest.
    try:
        callback_context = options['callback_context']
    except KeyError:
        pass
    else:
        future._invoke_callbacks = lambda: _alternate_invoke_callbacks(future, callback_context)
        future._callback_context_patched = True
    
    # Again, there may be a more appropriate way to implement this option, but
    # forcing a call to `result` is good enough for this example.
    if options.get('always_raise', False):
        future.add_done_callback(concurrent.futures.Future.result)
    
    return future
```

## Examples
This example is based on Greg Ewing's ["Spam Server"](http://www.cosc.canterbury.ac.nz/greg.ewing/python/tasks/SimpleScheduler.html) in his own essay on this subject. My version comes in two parts.

The first part is a context implementation. I consider this an example rather than implementation because it is not part of the API. There are many other equally valid implementations of a context, and this happens to be the one that I used for this example. It the zip file it is SingleThreadContext.py.

(Obviously there will need to be some standard context that is better than the default one above, but in most cases where it matters the specific implementation will be important enough that it should be a user decision and not forced upon them.)

```python
'''This is an example of a queuing loop that executes all submitted items on
the same thread it is run from. It also supports returning futures for
`SelectRead` and `SelectWrite` objects, which are thin wrappers around any
object that may be passed to `select.select`.
'''

import collections
import concurrent.futures
import select
import threading
from contexts import async, with_options, CallableContext


# These types are supported by SingleThreadContext.get_future_for
SelectRead = collections.namedtuple('SelectRead', ['fd'])
SelectWrite = collections.namedtuple('SelectWrite', ['fd'])

class SingleThreadContext(CallableContext):
    '''Represents a context for the current thread where callables may be
    executed.
    '''
    def __init__(self):
        self._terminate = False
        self._exit_code = None
        self._exit_exception = None
        self._main_thread = None
        self._read_sockets = {}
        self._write_sockets = {}
        self._signal = threading.Condition()
        self._queue = collections.deque()

    def submit(self, callable, *args, **kwargs):
        '''Adds a callable to invoke within this context.'''
        with self._signal:
            self._queue.append((callable, args, kwargs))
            self._signal.notify_all()

    def get_future_for(self, obj):
        '''Returns futures for socket objects.'''
        if isinstance(obj, (SelectRead, SelectWrite)):
            fd = obj[0]
            if fd in self._read_sockets or fd in self._write_sockets:
                # We cannot wait on the same file descriptor multiple times, but maybe the caller can.
                return None

            f = concurrent.futures.Future()
            f.set_running_or_notify_cancel()
            if isinstance(obj, SelectRead):
                self._read_sockets[fd] = f
            else:
                self._write_sockets[fd] = f
            return f
        return None

    def exit_with_future(self, future):
        '''Terminates the context, returning the result (or raising
        the exception) from the provided future.
        '''
        with self._signal:
            self._terminate = True
            self._exit_exception = future.exception()
            if not self._exit_exception:
                self._exit_code = future.result()
            self._signal.notify_all()

    def run(self, callable=None, *args, **kwargs):
        '''Starts the context. This method does not return until `exit` is
        called. The return value is the object passed in the call to `exit`.
        '''
        
        previous_context = CallableContext.set_current(self)
        
        if callable:
            future = callable(*args, **kwargs)
            # Add a callback if the returned object looks like a future.
            # If not, we silently forget about it.
            try:
                add_done_callback = future.add_done_callback
            except AttributeError:
                pass
            else:
                if hasattr(add_done_callback, "__call__"):
                    # If the future is already complete, the callback
                    # will be invoked immedately.
                    add_done_callback(self.exit_with_future)

            if self._terminate:
                # May have terminated already if callable returned a
                # completed future.
                return self._exit_code
    
        def _wait_condition():
            return self._terminate or self._queue or self._read_sockets or self._write_sockets

        # Main message loop
        while True:
            with self._signal:
                callable = None
                self._signal.wait_for(_wait_condition)

                if self._terminate:
                    break

                if self._read_sockets or self._write_sockets:
                    # Because we have no signal for another queued object, we will simply use a short
                    # timeout if _queue is not ready, and no timeout if it is.
                    # To do this properly would need a fake socket object on Windows (or any file
                    # descriptor on Linux) that we can make ready if a completion occurs.
                    ready_read, ready_write, _ = select.select(
                        self._read_sockets.keys(),
                        self._write_sockets.keys(),
                        [],
                        0 if self._queue else 0.01
                    )
                    for rr in ready_read:
                        future = self._read_sockets.pop(rr)
                        future.set_result(rr)
                    for rw in ready_write:
                        future = self._write_sockets.pop(rw)
                        future.set_result(rw)
                    if ready_read or ready_write:
                        continue

                if self._queue:
                    callable, args, kwargs = self._queue.popleft()

            if callable:
                # Call the task outside of the lock
                callable(*args, **kwargs)

        CallableContext.set_current(previous_context)

        if self._exit_exception:
            raise self._exit_exception
        return self._exit_code
```

The second part is the actual server, implemented using `@async` functions. It also includes implementations for `sock_accept_async`, `sock_readline_async` and `sock_write_async` that use the `get_future_for` feature of `SingleThreadContext`. This code is in the SocketSpam.py file, and has only been tested on Windows (where it works fine).

```python
# Based on the example by Greg Ewing

#   http://www.cosc.canterbury.ac.nz/greg.ewing/python/tasks/SimpleScheduler.html

from contexts import CallableContext, async
from socket import *
from SingleThreadContext import *

port = 4200

@async
def sock_accept_async(sock):
    f = CallableContext.get_current().get_future_for(SelectRead(sock))
    if f:
        return (yield f).accept()
    else:
        # This is the fallback option for when the current context does not support `SelectRead`
        # objects. It should probably use a CPU thread rather than blocking the caller.
        return sock.accept()

@async
def sock_readline_async(sock):
    buf = b""
    while buf[-1:] != b"\n":
        f = CallableContext.get_current().get_future_for(SelectRead(sock))
        if f:
            data = (yield f).recv(1024)
            # Uncomment this exception to see how with_options(f, always_raise=True) handles
            # errors in async functions that are not waited upon.
            #raise Exception("EPIC FAIL!")
        else:
            # This is the fallback option for when the current context does not support `SelectRead`
            # objects. It should probably use a CPU thread rather than blocking the caller.
            data = sock.recv(1024)
        if not data:
            break
        buf += data
    if not buf:
        sock.close()
    return buf.decode(errors='ignore')

@async
def sock_write_async(sock, data):
    data = data.encode()
    while data:
        f = CallableContext.get_current().get_future_for(SelectWrite(sock))
        if f:
            n = (yield f).send(data)
        else:
            # This is the fallback option for when the current context does not support `SelectWrite`
            # objects. It should probably use a CPU thread rather than blocking the caller.
            n = sock.send(data)
        data = data[n:]

@async
def listener():
    lsock = socket(AF_INET, SOCK_STREAM)
    lsock.setsockopt(SOL_SOCKET, SO_REUSEADDR, 1)
    lsock.bind(("", port))
    lsock.listen(5)
    while 1:
        csock, addr = yield sock_accept_async(lsock)
        print("Listener: Accepted connection from", addr)
        yield sock_write_async(csock, "Welcome to my Spam Machine!\r\n")

        # Calling without yielding the returned future runs multiple tasks in parallel
        # But we apply the `always_raise` option to the future to ensure we hear about
        # any errors. A more robust program would handle them in a better way than this.
        with_options(handler(csock), always_raise=True)

@async
def handler(sock):
    while 1:
        line = yield sock_readline_async(sock)
        if not line:
            break
        try:
            n = parse_request(line)
            yield sock_write_async(sock, "100 SPAM FOLLOWS\r\n")
            for i in range(n):
                yield sock_write_async(sock, "spam glorious spam\r\n")
        except BadRequest:
            yield sock_write_async(sock, "400 WE ONLY SERVE SPAM\r\n")

class BadRequest(Exception):
    pass

def parse_request(line):
    tokens = line.split()
    if len(tokens) != 2 or tokens[0] != "SPAM":
        raise BadRequest
    try:
        n = int(tokens[1])
    except ValueError:
        raise BadRequest
    if n < 1:
        raise BadRequest
    return n


SingleThreadContext().run(listener)
```

# Summary
If you've made it this far, well done. This became a much longer essay than I originally planned. Very few (probably none) of these ideas are new, and all of them have appeared in the discussion to date, but I wanted to take the time to collate it all into one place. While I have presented everything here as fact, that is simply my preferred style - assume an implicit "IMO" before each statement - and I am willing to be convinced that another approach is better. I openly acknowledge that I have very little experience with the `select` model, but nothing that I've learned about it in putting this together has made me want to use it in real life.

I intend to post a link to this to the [python-ideas](https://mail.python.org/mailman/listinfo/python-ideas) list (rather than the full content, obviously!) and that is the appropriate place to continue the discussion. If you are reading this long after October 2012, this may be of historical interest only, but feel free to comment if you so desire.
