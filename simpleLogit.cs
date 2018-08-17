using System;
using System.Activities;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data;
using Accord.Statistics.Models.Regression;
using Accord.Statistics.Models.Regression.Fitting;
using Accord.Math.Optimization.Losses;
using Accord.Statistics.Testing;

namespace Ciphix.MachineLearning
{
    public class SimpleLogit : CodeActivity
    {
        [Category("Input"), Description("The datatable with the binary dependent variable (first column) and the regressor variable(s)"), RequiredArgument]
        public InArgument<DataTable> DataTable { get; set; }

        [Category("Input"), Description("The array with regressor values needed to predict the binary dependent variable"), RequiredArgument]
        public InArgument<double[]> inputValues { get; set; }

        [Category("Output"), Description("A boolean representing the binary choice predicted by the logit model")]
        public OutArgument<bool> Result { get; set; }

        [Category("Output"), Description("The structure with diagnostics for the model and prediction. Attributes: Summary, Regressors, Statistics, Prediction, PredictionStdError")]
        public OutArgument<Diagnostics> Diagnostics { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var table = DataTable.Get(context);
            var inRow = inputValues.Get(context);

            int numberOfRows = table.Rows.Count;
            if (numberOfRows == 0)
            {
                throw new System.ArgumentException("Please supply at least 1 row");
            }

            int numberOfCols = table.Columns.Count;
            if (numberOfCols < 2)
            {
                throw new System.ArgumentException("Please supply at least 2 columns");
            }

            //get all the y's
            bool[] y = new bool[numberOfRows];
            double[] yDouble = new double[numberOfRows];
            int i = 0;
            foreach (DataRow dtRow in table.Rows)
            {
                if (dtRow[0] is string)
                {
                    if (dtRow[0].ToString().ToLower() == "false")
                    {
                        y[i] = false;
                        yDouble[i] = 0.0;
                        i++;
                    }
                    else if (dtRow[0].ToString().ToLower() == "true")
                    {
                        y[i] = true;
                        yDouble[i] = 1.0;
                        i++;
                    }
                    else
                    {
                        throw new System.ArgumentException("The first column can contain only 0 or 1");
                    }
                }
                else if (dtRow[0] is bool || dtRow[0] is double || dtRow[0] is int)
                {
                    y[i] = Convert.ToBoolean(dtRow[0]);
                    yDouble[i] = Convert.ToDouble(dtRow[0]);
                }
                else
                {
                    throw new System.ArgumentException("The first column can contain only 0 or 1");
                }
            }

            double[][] input = new double[numberOfRows][];
            i = 0;
            foreach (DataRow dtRow in table.Rows)
            {
                input[i] = new double[numberOfCols - 1];
                for (int j = 1; j < numberOfCols; j++)
                {
                    if (dtRow[j] is string)
                    {
                        double variable;
                        if (!double.TryParse(dtRow[j].ToString(), out variable))
                        {
                            throw new System.ArgumentException("All input arguments must be convertable to double");
                        }
                        else
                        {
                            input[i][j - 1] = variable;
                        }
                    }
                    else if (dtRow[j] is bool || dtRow[j] is double || dtRow[j] is int)
                    {
                        input[i][j - 1] = Convert.ToDouble(dtRow[j]);
                    }
                    else
                    {
                        throw new System.ArgumentException("All input arguments must be convertable to double");
                    }
                }
                i++;
            }

            var learner = new IterativeReweightedLeastSquares<LogisticRegression>()
            {
                Tolerance = 1e-4,  // Let's set some convergence parameters
                MaxIterations = 100,  // maximum number of iterations to perform
                Regularization = 0
            };

            LogisticRegression regression = learner.Learn(input, y);
            bool hasConv = learner.HasConverged;
            int iterations = learner.CurrentIteration;
            double[][] fisherI = learner.GetInformationMatrix();

            var learnerIntercept = new IterativeReweightedLeastSquares<LogisticRegression>()
            {
                Tolerance = 1e-4,  // Let's set some convergence parameters
                MaxIterations = 100,  // maximum number of iterations to perform
                Regularization = 0
            };

            double[][] dataIntercept = new double[numberOfRows][];
            for (i = 0; i < numberOfRows; i++)
            {
                dataIntercept[i] = new double[1];
                dataIntercept[i][0] = 0;
            }
            LogisticRegression regrIntercept = learnerIntercept.Learn(dataIntercept, y);
            double loglikInter = regrIntercept.GetLogLikelihood(dataIntercept, yDouble);


            if (inRow.Count() != numberOfCols - 1)
            {
                throw new System.ArgumentException("Please supply the correct amount of elements for the prediction");
            }

            double[][] predIn = new double[1][];
            predIn[0] = inRow;
            bool[] pred = regression.Decide(predIn);

            //create the diags
            double inter = regression.Intercept;
            double[] est = new double[regression.Weights.Length + 1];
            est[0] = regression.Intercept;
            regression.Weights.CopyTo(est, 1);

            double[] se = regression.StandardErrors;
            double predSe = regression.GetPredictionStandardError(inRow, fisherI);

            double chi2p = regression.ChiSquare(input, yDouble).PValue;
            double chi2stat = regression.ChiSquare(input, yDouble).Statistic;
            double[] probs = regression.Probabilities(inRow);
            double loglik = regression.GetLogLikelihood(input, yDouble);

            double[] score = regression.Probability(input);
            double loglikLoss = new LogLikelihoodLoss().Loss(score);

            double[] oddsRatios = new double[numberOfCols];
            double[][] waldTests = new double[2][];
            waldTests[0] = new double[numberOfCols];
            waldTests[1] = new double[numberOfCols];

            for (i = 0; i < numberOfCols; i++)
            {
                oddsRatios[i] = regression.GetOddsRatio(i);

                WaldTest wald = regression.GetWaldTest(i);
                waldTests[0][i] = wald.PValue;
                waldTests[1][i] = wald.Statistic;
            }

            //construct the data
            Dictionary<string, string> Summary = new Dictionary<string, string>();
            Summary.Add("Dependent Variable", table.Columns[0].ColumnName);
            Summary.Add("Method", "Iterative Reweighted Least Squares");
            Summary.Add("Sample Size", numberOfRows.ToString());
            Summary.Add("Convergence", hasConv.ToString());
            Summary.Add("Iterations", iterations.ToString());

            DataTable coefTable = new DataTable();
            coefTable.Columns.Add("Variable", typeof(string));
            coefTable.Columns.Add("Coefficient", typeof(double));
            coefTable.Columns.Add("Standard Error", typeof(double));
            coefTable.Columns.Add("z-Statistic", typeof(double));
            coefTable.Columns.Add("Probability", typeof(double));
            coefTable.Columns.Add("Odds Ratio", typeof(double));
            for (i = 0; i < numberOfCols; i++)
            {
                if (i == 0)
                {
                    coefTable.Rows.Add("C", est[0], se[0], waldTests[1][0], waldTests[0][0], oddsRatios[0]);
                }
                else
                {
                    coefTable.Rows.Add(table.Columns[i].ColumnName, est[i], se[i], waldTests[1][i], waldTests[0][i], oddsRatios[i]);
                }
            }

            Dictionary<string, double> Statistics = new Dictionary<string, double>();
            Statistics.Add("McFadden R-squared", 1 - (loglik / loglikInter));
            Statistics.Add("AIC", 2 * (numberOfCols) - 2 * loglik);
            Statistics.Add("BIC", Math.Log(numberOfRows) * (numberOfCols) - 2 * loglik);
            Statistics.Add("HQC", -2 * loglik + 2 * (numberOfCols) * Math.Log(Math.Log(numberOfRows)));

            Statistics.Add("Log likelihood", loglik);
            Statistics.Add("LR Statistic", chi2stat);
            Statistics.Add("Prob(LR Statistic)", chi2p);

            Statistics.Add("Log likelihood Loss", loglikLoss);

            Diagnostics diags = new Diagnostics { Summary = Summary, Regressors = coefTable, Statistics = Statistics, Prediction = probs, PredictionStdError = predSe };

            Diagnostics.Set(context, diags);
        }
    }

    public struct Diagnostics
    {
        public Dictionary<string, string> Summary;
        public DataTable Regressors;
        public Dictionary<string, double> Statistics;
        public double[] Prediction;
        public double PredictionStdError;
    }

}

