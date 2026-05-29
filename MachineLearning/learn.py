from sklearn import tree
from sklearn.metrics import accuracy_score
from sklearn.model_selection import train_test_split
import pandas as pd
import pickle

# read the input data
data = pd.read_csv('it_jobs_automl_dataset.csv')

# prepare personal data: remove everything except skills
X = data.drop('got_job', axis=1)
X = X.drop('name', axis=1)

# split off the classification column
y = data['got_job']

# TODO: split the table into training and test data

# TODO: fit a decision tree to the training data

# TODO: test the model

# TODO: measure accuracy

# TODO: save the model to file
