unit ToolsUnit4;


interface

uses ToolsUnit1, {ToolsUnit2, }ToolsUnit3,
      {$IFDEF USE_DELPHI_BCD}
      FMTBcd;
      {$ELSE}
      StBcd;
      {$ENDIF}


implementation


uses
  SysUtils//, ToolsUnit1
  ;


end.
