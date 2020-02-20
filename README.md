---
title: Steve Dower | Musings and Mutterings
permalink: /
is_root: true
---

[Latest Blog Post]({{ site.posts.first.url }}) [Speaking](/speaking)

## {{ site.posts.first.title }}

{{ site.posts.first.excerpt }}

[Continue reading...]({{ site.posts.first.url }})

## Other Posts

{% for post in site.posts %}
<strong>{{ post.date | date_to_string }}</strong> - [{{post.title}}]({{post.url}})
{% endfor %}
