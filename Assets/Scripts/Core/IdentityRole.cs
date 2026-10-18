namespace FingerDiceDuel.Core
{
    /// <summary>8 人身份局的身份。</summary>
    public enum IdentityRole
    {
        None = 0,
        Lord = 1,
        Loyalist = 2,
        Rebel = 3,
        Traitor = 4,
    }

    public static class IdentityRoleInfo
    {
        public static string DisplayName(IdentityRole role)
        {
            switch (role)
            {
                case IdentityRole.Lord: return "主公";
                case IdentityRole.Loyalist: return "忠臣";
                case IdentityRole.Rebel: return "反贼";
                case IdentityRole.Traitor: return "内奸";
                default: return "？";
            }
        }

        public static bool IsLordSide(IdentityRole role)
            => role == IdentityRole.Lord || role == IdentityRole.Loyalist;

        public static bool IsRebelSide(IdentityRole role)
            => role == IdentityRole.Rebel;

        public static bool IsTraitor(IdentityRole role)
            => role == IdentityRole.Traitor;
    }
}
