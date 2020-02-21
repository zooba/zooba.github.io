---
layout: page
title: ESDL Grammar
description: A BNF-style grammar for ESDL.
slug: esdlgrammar
redirect_from: /blog/research/esdlgrammar
sort_key: G
---

ESDL is very much still under development. Part of this development process requires community feedback and engagement. Ideally, a number of alternate implementations will be constructed, allowing a greater range of researchers to experiment with ESDL.

However, implementation is difficult. The whole point of ESDL is to support algorithm sharing within the Evolutionary Computation community. At present, the only (known) implementation of ESDL is in [esec](https://github.com/zooba/esec).

This page contains a [BNF-style grammar](https://en.wikipedia.org/wiki/BNF_grammar) describing ESDL in its current form. This is anticipated to change over time based on feedback from users. It is labelled "BNF-style" because this grammar can't be used directly with any parser generator; it is intended as a specification for humans, not machines.

Any feedback can be sent to the email shown [here](/about).

```
System      : Statements EOS (BlockStmt EOS)*

EOS         : <a new line, except when immediately after a backslash>

Statement   : RepeatStmt EOS
            | FromStmt EOS
            | JoinStmt EOS
            | YieldStmt EOS
            | EvalStmt EOS
            | PragmaStmt EOS
            | AssignStmt EOS
            | CallFunc EOS

Statements  : <zero or more Statement>

BlockStmt   : "BEGIN" Name EOS Statements "END"

RepeatStmt  : "REPEAT" Expression EOS Statements "END"

FromStmt    : "FROM" GroupOrGenerators "SELECT" SizedGroups ("USING" Operators)?
JoinStmt    : "JOIN" Groups "INTO" Groups ("USING" Operators)?

YieldStmt   : "YIELD" Groups

EvalStmt    : "EVAL"     Groups ("USING" Operators)?
            | "EVALUATE" Groups ("USING" Operators)?

PragmaStmt  : "`" <anything> EOS

AssignStmt  : Name "=" Expression

Expression  : Operand (BinaryOp Operand)*

Operand     : Name                          // variable
            | CallFunc
            | Number                        // constant
            | "(" Expression ")"
            | UnaryOp Operand
            | "true"
            | "false"
            | "null" | "none"

UnaryOp     : "-"
BinaryOp    : "+" | "-" | "*" | "/" | "^" | "."

Parameter   : Name "=" Expression           // explicit parameter
            | Name                          // implicit parameter, see [TR/CIS/2010/7](/publications#esdl_implicit_parameter_proposal_2010)

CallFunc    : Name "(" Parameter ("," Parameter)* ")"

Groups      : Name (',' Groups)*
SizedGroups : Expression Name (',' SizedGroups)* // sized group
            |            Name (',' SizedGroups)* // unsized group

GroupsOrGenerators
            : Name      ("," GroupsOrGenerators)*
            | CallFunc  ("," GroupsOrGenerators)*

Operators   : Name     ("," Operators)*
            | CallFunc ("," Operators)*
```
