using Giventocode.MobileServices.AzureSearch;
using Microsoft.WindowsAzure.Mobile.Service;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Giventocode.AzureSearch.DataObjects
{
    [Table("dbo.RockBands")]
    public class RockBand:EntityData
    {
        [Indexable("Edm.String", true, Suggestions = true, Retrievable = true, Facetable = false)]
        public string Name { get; set; }

        [Indexable("Edm.String", true,Facetable=false)]
        public string Description { get; set; }

        [Indexable("Edm.String", true)]
        public string Genre { get; set; }
    }
}