#LogitModel

## Inspiration

Automation technologies of any kind have been disrupting industries for decades to date. The technological improvements during the 19th century have had great impact on human life, yet most of the innovation was limited to substitute human strength and dexterity. New disruptive technologies such as Artiﬁcial Intelligence and Robotic Process Automation enable a by far larger scope of automation eﬀorts. Robotic Process Automation enables organizations to deploy software robots that are able to execute manual tasks across any existing application. Suitable processes to be automated by software robots are often repetitive, highly standardized and rule-based. Yet, Robotic Process Automation has the potential to automate a wider range of processes if it utilizes cognitive capabilities through Artiﬁcial Intelligence.

This is a first step to cognitive capabilities within a robot by implementing a Simple Logit Model with advanced statistical diagnostics.

## What it does

The goal of this custom activity is to perform a forecast where the variable we want to predict (dependent variable) is either a 0 or a 1 (binary).

**Input**

The activity supports two required input variables, _ DataTable _ and _ inputValues _. DataTable is a datatable which contains all data required to train the model. 

_DataTable_ is contains a data set that enables you to train your model. The first column of DataTable should contain the dependent variable and the remainder columns should contain regressors. The first column should be some datatype that is easy to convert to a Boolean (int (0/1), string (false/true), double(0.0/1.0), the remainders columns should be numbers (supports: int, string, float) which are converted to doubles. If a value cannot be converted into the right datatype, the activity will throw an error. 

The array inputValues is an array containing the data you want to use to predict an outcome. This array can only consist of doubles and should have the same dimension as the number of regressors in DataTable, otherwise an error is thrown.

**Processing**

The processing works in three steps: validation; estimation; diagnostics. First the datatable is  converted to a multidimensional double array and a one dimensional double array, the first containing all regressor data and the latter containing only the dependent variable data. The dimension of the inputValues array is also validated. After the validation, we estimate the logit model with the **Iterative Reweighted Least Squares** method. 

Note
Using the Least Square method results in losing explanation capability within the model but we gain prediction accuracy which is the ultimate goal of this activity. After the estimation is done, we store diagnostics for understanding and further processing.

**Output**

Results and diagnostics
```
struct Diagnostics
{
    Dictionary<string, string> Summary
    {
        "Dependent Variable",
        "Method",
        "Sample Size",
        "Convergence",
        "Iterations"
    },
    DataTable Regressors
    {
        "Variable": typeof(string),
        "Coefficient": typeof(double),
        "Standard Error": typeof(double),
        "z-Statistic": typeof(double),
        "Probability": typeof(double),
        "Odds Ratio": typeof(double),
    },
    Dictionary<string, double> Statistics
    {
        "McFadden R-squared",
        "AIC",
        "BIC",
        "HQC",
        "Log likelihood",
        "LR Statistic",
        "Prob(LR Statistic)",
        "Log likelihood Loss",
    },
    double[] Prediction,
    double PredictionStdError
}
```

## How we built it

Built in Visual Studio using C# implementing a CodeActivity with support of the Accord.Statistics Framework ([Accord MSDN](http://accord-framework.net/docs/html/N_Accord_Statistics.htm))

## Accomplishments that we're proud of

This model comprises of a very inclusive set of diagnostics such as akaike information criteria but also an LR test statistic which is tested on the Logit model against a Logit model with only an intercept. We have tested this model on various datasets from kaggle.com and were able to make accurate predictions on business data ([To loan, or not to loan](https://www.kaggle.com/c/to-loan-or-not-to-loan/data)). 

## What we learned

We have learned the ease of implementing custom code into Custom Activities for UiPath. Although this model is not as fancy as the ML / DL models that fascinates the academic society these days. The results of this model are pretty awesome, check them out in Demo_Results on Github.

## What's next for Machine Learning - Logistic Regression Model Activity

1. Enabling our activity to engage in self learning within its own model
2. Independently selecting its own regressors from the dataset to prevent overfitting 
3. Supply the user with more advanced prediction quality measures


## Background information

A Logit model describes a non-linear regression,in this short explanation we assume that the reader knows what regression is. The goal is to perform a forecast where the variable we want to predict (dependent variable) is either a 0 or a 1 (binary). We could perform a standard linear regression, however since the linear model works better on real dependent variables and not binary ones we have come up with a better solution.

The red line in the left pane of the image shown above is a linear model used on a binary dependent variable, this is a linear probability model (LPM). Note that the fit of this line is bad and rather awkward. Instead of a linear relation between the dependent variable and the regressors, the logit model uses the logistic probability function to determine the relationship between the dependent and the independent variables. Hence the values of the estimates of the dependent variable can be interpreted as the probability of the dependent variable attaining the value 1. We are now able to properly specify a model for a binary dependent variable, hence we are also able to make predictions with the model, which satisfies our main objective of this custom UiPath activity.
