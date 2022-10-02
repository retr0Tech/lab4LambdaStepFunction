using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWSServerlessLab4
{
    public class RekognitionResponse
    {
        public string Label { get; set; }
        public float Confidence { get; set; }
    }
}
