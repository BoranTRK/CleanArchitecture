using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.CrossCuttingConcerns.Logging;


// Loglarla ilgili detaylar bu sınıf içerisinde yer alacak
public class LogDetail
{
    public string FullName { get; set; } // İşlemin yapıldığı sınıfın tam adı
    public string MethodName { get; set; }// İşlemin çalıştığı metodun adı
    public string User { get; set; } // İşlemi yapan kullanıcı
    public List<LogParameter> Parameters { get; set; }

    public LogDetail()
    {
        FullName = string.Empty;
        MethodName = string.Empty;
        User = string.Empty;
        Parameters = new List<LogParameter>();
    }

    public LogDetail(string fullName, string methodName, string user, List<LogParameter> parameters)
    {
        FullName = fullName;
        MethodName = methodName;
        User = user;
        Parameters = parameters;
    }
}
