using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Entidades
{
    public class InfoCell
    {
        public long Mcc { get; set; }
        public long Mnc { get; set; }
        public string Lac { get; set; }
        public string Cellid { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public double senialdB { get; set; }
        public int TecnologiaAcceso { get; set; }
        public int Canal { get; set; }
        public string Banda { get; set; }
        public DateTime FechaHora { get; set; }
        public string NumeroRegistro { get; set; }
        public int AverageSignalStrength { get; set; }
        public int Range { get; set; }
        public int Samples { get; set; }
        public int Changeable { get; set; }
        public string Radio { get; set; }
        public int Rnc { get; set; }
        public int Cid { get; set; }
        public int Tac { get; set; }
        public int Sid { get; set; }
        public int Nid { get; set; }
        public int Bid { get; set; }
        public string Message { get; set; }

        public bool IsInDatabase { get; set; }

        public ParametrosModelo ParametrosModelo { get; set; }

        public InfoCell()
        {
            Mcc = 0;
            Mnc = 0;
            Lac = string.Empty;
            Cellid = string.Empty;
            senialdB = 0;
            TecnologiaAcceso = 0;
            Canal = 0;
            Banda = string.Empty;
            FechaHora = DateTime.Now;
            NumeroRegistro = string.Empty;
            IsInDatabase = false;
            Radio = string.Empty;

            ParametrosModelo = new ParametrosModelo();
        }

        //Contructor a partir de un CellInfo recibido  
        public InfoCell(CellInfoRemote cellInfo)
        {
            Mcc = cellInfo.Mcc;
            Mnc = cellInfo.Mnc;
            Lac = cellInfo.Lac;
            Cellid = cellInfo.CellId;
            senialdB = cellInfo.SignalLevel;
            TecnologiaAcceso = cellInfo.Technology;
            Canal = cellInfo.Channel;
            Banda = cellInfo.Band ?? string.Empty; 
            FechaHora = DateTime.Now;
            NumeroRegistro = string.Empty;
            IsInDatabase = false;
            Radio = string.Empty;
        }
    }
}
