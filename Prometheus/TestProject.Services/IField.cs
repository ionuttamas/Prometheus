namespace TestProject.Services
{
    public interface IField
    {
        decimal Compute(decimal delta);
    }

    public class CurrentPriceField : IField {
        public decimal Compute(decimal delta) {
            return delta;
        }
    }

    public class AskField : IField {
        private readonly decimal defaultValue;

        public AskField(decimal value) {
            defaultValue = value;
        }

        public decimal Compute(decimal delta) {
            if (delta < 0)
                return defaultValue;

            return delta;
        }
    }

    public class BidField : IField {
        private readonly decimal defaultValue;

        public BidField(decimal value) {
            defaultValue = value;
        }

        public decimal Compute(decimal delta) {
            if (delta > 0)
                return defaultValue;

            return delta;
        }
    }
}