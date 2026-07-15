namespace _Scripts.Utilities
{
    public static class Constants
    {
        //  
        public static float SPEED_COEFFICIENT = 0.09F;
        public static float SENSITIVITY_COEFFICIENT = 0.07F;
        
        // CLamp the translate on x
        public static float CLAMP_MODIFIER = 2.2F;
        public static float MAX_SCORE = 100F;
        
        
        public static float LERP_COEF = 0.3F;
        public static float SWERVE_SPEED = 1250F;
        
        public enum CorridorTypes
        {
            Increase,
            Decrease,
            Multiply,
            Divide
        }
        
    }
}
