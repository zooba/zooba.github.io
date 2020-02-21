---
layout: page
title: Research
permalink: /research
redirect_from: /blog/research
---

Pages in this section:
<dl>
{% for p in site.research.docs %}
{% if p.url != page.url %}
<dt><a href="{{ p.url }}">{{ p.title }}</a></dt>
<dd>{{ p.description | markdownify }}</dd>
{% endif %}
{% endfor %}
<dt><a href="/research/esdl">Evolutionary System Definition Language (ESDL)</a></dt>
<dd>An overview page of ESDL, which was a central part of my Ph.D. work.</dd>
<dt><a href="/research/esdl-latex">ESDL Formatting (LaTeX)</a></dt>
<dd>Some <a href="http://www.latex-project.org/">LaTeX</a> code for rendering ESDL listings.</dd>
<dt><a href="/research/esdlgrammar">ESDL Grammar</a></dt>
<dd>A BNF-style grammar for ESDL.</dd>
</dl>