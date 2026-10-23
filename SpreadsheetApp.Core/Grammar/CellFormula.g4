// Cell formula grammar. Precedence, lowest to highest:
// eqv, or, and, not, comparison, + -, * / div mod, unary + -, ^, primary.

grammar CellFormula;

options { caseInsensitive = true; }

// Parser rules

formula        : EQ? eqvExpr EOF ;

eqvExpr        : orExpr (EQV orExpr)* ;

orExpr         : andExpr (OR andExpr)* ;

andExpr        : notExpr (AND notExpr)* ;

notExpr        : NOT notExpr                                # LogicalNot
               | comparison                                 # PlainComparison
               ;

comparison     : additive (op+=(EQ | NEQ | LT | LE | GT | GE) additive)* ;

additive       : multiplicative (op+=(PLUS | MINUS) multiplicative)* ;

multiplicative : unary (op+=(MUL | SLASH | DIV | MOD) unary)* ;

unary          : sign=(PLUS | MINUS) unary                  # SignedUnary
               | power                                      # PlainPower
               ;

// Right-associative: 2 ^ 3 ^ 2 = 2 ^ 9
power          : primary (CARET unary)? ;

primary        : NUMBER                                     # NumberAtom
               | CELL                                       # CellAtom
               | IDENT LPAREN (eqvExpr (COMMA eqvExpr)*)? RPAREN   # FunctionAtom
               | LPAREN eqvExpr RPAREN                      # ParenAtom
               ;

// Lexer rules (keywords come before IDENT so they win ties)

NOT      : 'not' ;
AND      : 'and' ;
OR       : 'or' ;
EQV      : 'eqv' ;
MOD      : 'mod' ;
DIV      : 'div' ;

CELL     : [A-Z]+ [0-9]+ ;
IDENT    : [A-Z]+ ;
NUMBER   : [0-9]+ ;

// Malformed words like 1A or A1B, reported with a clear message
BAD_WORD : [0-9]+ [A-Z] [A-Z0-9]*
         | [A-Z]+ [0-9]+ [A-Z] [A-Z0-9]*
         ;

LE       : '<=' ;
GE       : '>=' ;
NEQ      : '<>' ;
LT       : '<' ;
GT       : '>' ;
EQ       : '=' ;
PLUS     : '+' ;
MINUS    : '-' ;
MUL      : '*' ;
SLASH    : '/' ;
CARET    : '^' ;
LPAREN   : '(' ;
RPAREN   : ')' ;
COMMA    : [,;] ;

WS       : [ \t\r\n]+ -> skip ;

// Any other character becomes a parser error
UNKNOWN  : . ;
