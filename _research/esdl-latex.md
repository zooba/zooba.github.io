---
layout: page
title: ESDL Formatting (LaTeX)
description: Some [LaTeX](https://www.latex-project.org/) code for rendering ESDL listings.
slug: esdl-latex
redirect_from: /blog/research/esdl-latex
sort_key: F
---

Most publications in this field (and computer science more generally) are created using [LaTeX](https://www.latex-project.org/). To simplify/assist/encourage the inclusion of ESDL in publications, this page provides settings for the [listings](https://www.ctan.org/tex-archive/macros/latex/contrib/listings/) package for an ESDL environment. At present, this is a little hacked together, but over time will be improved (feedback is very welcome). Anyone is welcome to freely use and/or modify these settings for their own purposes (no attribution required).

An example (click for larger image):

[![ESDL Sample](/assets/esdl_pso_small.png)](/assets/esdl_pso.png)

Listings are introduced using either of the following two lines, depending on whether line numbers are desired. I usually put listings within a `\begin{minipage}{0.95\textwidth}` or `\begin{minipage}{\columnwidth}`.

{% raw %}
```
\begin{esdl}[label={label},caption={caption},style=nonumbers]}
\begin{esdl}[label={label},caption={caption},style=numbers]}
</pre>
The definition below should be included in your preamble. It uses @ as a LaTeX escape, allowing labels and crossreferences in the listing.
<pre>\usepackage{color}
\usepackage{listings}

\definecolor{lstgray}{gray}{0.4}
\definecolor{lstdarkpurple}{rgb}{0.2,0,0.5}
\definecolor{lstdarkblue}{rgb}{0,0,0.6}

\lstdefinestyle{numbers}{numbers=left,numberstyle=\tiny,stepnumber=1,numbersep=3pt}
\lstdefinestyle{nonumbers}{numbers=none}

\lstnewenvironment{esdl}[1][]{
\lstset{
language=python,
basicstyle=\ttfamily\footnotesize\setstretch{1},
stringstyle=\color{lstdarkpurple},
showstringspaces=false,
alsoletter={1234567890},
keywordstyle=\color{black},
emph={FROM,SELECT,USING,JOIN,INTO,YIELD,BEGIN,END,REPEAT,EVAL,EVALUATE},
emphstyle=\color{blue}\bfseries,
upquote=true,
commentstyle=\color{lstgray}\sffamily,
morecomment=[l]{//},
morecomment=[l]{;},
morecomment=[l]{\#},
literate=*{(}{{\textcolor{lstdarkblue}(}}{1}%
{)}{{\textcolor{lstdarkblue})}}{1}%
{-}{{\textcolor{lstdarkblue}-}}{1}%
{+}{{\textcolor{lstdarkblue}+}}{1}%
{*}{{\textcolor{lstdarkblue}*}}{1}%
{=}{{\textcolor{lstdarkblue}=}}{1},%
framexleftmargin=1mm, framextopmargin=1mm, 
frame=Tb,%rulesepcolor=\color{blue},
#1 }}{}
```
{% endraw %}
