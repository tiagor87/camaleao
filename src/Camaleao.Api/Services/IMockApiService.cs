﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Camaleao.Api
{
    public interface IMockApiService
    {
         Task Invoke(HttpContext context, RequestDelegate next);
    }
}
