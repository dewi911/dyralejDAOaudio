using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dyralejDAOaudio
{
    public interface IEffect
    {
        float ApplyEffect(float sample);
    }
}
