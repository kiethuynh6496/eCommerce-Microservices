using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Identity.Application.DTOs.Auth;

public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);
