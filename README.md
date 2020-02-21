---
title: Steve Dower | Musings and Mutterings
permalink: /
is_root: true
---

[Speaking](/speaking) | [Research](/research)

## Latest Post: {{ site.posts.first.title }}

{{ site.posts.first.excerpt }}

[Continue reading...]({{ site.posts.first.url }})

## My Pages

*[Speaking](/speaking)* - My talk history and recordings

*[Research](/research)* - My (past) research and publications

## Other Posts

{% for post in site.posts %}
<strong>{{ post.date | date_to_string }}</strong> - [{{post.title}}]({{post.url}})
{% endfor %}
