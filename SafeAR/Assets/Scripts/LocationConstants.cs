using System.Collections;
using System.Collections.Generic;
using Mapbox.Unity.Utilities;
using Mapbox.Utils;
using UnityEngine;

public static class LocationConstants
{
    //Buildings 
    public static readonly Vector2d patioA = Conversions.StringToLatLon("39.73562884673216, -8.821239179407588");
    public static readonly Vector2d edificioA = Conversions.StringToLatLon("39.73597949201858, -8.820383554813013");
    public static readonly Vector2d edificioDCentro = Conversions.StringToLatLon("39.73443761765348, -8.82091988370953");
    public static readonly Vector2d edificioDDireita = Conversions.StringToLatLon("39.734098555233544, -8.820704493472434");
    public static readonly Vector2d edificioDEsquerda = Conversions.StringToLatLon("39.734805049530415, -8.820423524047841");
    public static readonly Vector2d edificioB = Conversions.StringToLatLon("39.734555284772355, -8.821614406786127");
    public static readonly Vector2d edificioC = Conversions.StringToLatLon("39.73380771775489, -8.822028353513716");
    public static readonly Vector2d edificioE = Conversions.StringToLatLon("39.733053692576526, -8.821684608456614");
    public static readonly Vector2d esslei = Conversions.StringToLatLon("39.732562767841216, -8.820394466088407");
    public static readonly Vector2d biblioteca = Conversions.StringToLatLon("39.73339816289226, -8.82066805138197");


    //Parking Lots
    public static readonly Vector2d estacionamentoADireita = Conversions.StringToLatLon("39.73526507618657, -8.819985039948746");
    public static readonly Vector2d estacionamentoAEsquerda = Conversions.StringToLatLon("39.73502918884651, -8.820607546834667");
    public static readonly Vector2d estacionamentoDEsquerda = Conversions.StringToLatLon("39.73399413480477, -8.821375864039926");
    public static readonly Vector2d estacionamentoDDireita = Conversions.StringToLatLon("39.733730112091514, -8.821096914328843");
    public static readonly Vector2d estacionamentoAtrasE = Conversions.StringToLatLon("39.732960870943096, -8.82218886374913");
    public static readonly Vector2d estacionamentoE = Conversions.StringToLatLon("39.733016563939834, -8.821145484541324");
    public static readonly Vector2d estacionamentoESSLEI = Conversions.StringToLatLon("39.73266333291364, -8.819939930841864");
    public static readonly Vector2d estacionamentoProfessores = Conversions.StringToLatLon("39.73617739757694, -8.821387936004108");
    public static readonly Vector2d dakar = Conversions.StringToLatLon("39.73528536580715, -8.822499704716488");

    //Food Places
    public static readonly Vector2d cantinaBaixo = Conversions.StringToLatLon("39.734546938567014, -8.82264186178359");
    public static readonly Vector2d cantinaCima = Conversions.StringToLatLon("39.7333843339217, -8.821592821572091");
    public static readonly Vector2d barA = Conversions.StringToLatLon("39.73560409523277, -8.820657140106576");

    //Green Areas
    public static readonly Vector2d zonaVerdeDakar = Conversions.StringToLatLon("39.73516235033306, -8.821776799950166");
    public static readonly Vector2d zonaVerdeEdificioC = Conversions.StringToLatLon("39.734098387240664, -8.822327167822998");
}
