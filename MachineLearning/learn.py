from sklearn import tree
from sklearn.metrics import accuracy_score
from sklearn.model_selection import train_test_split
import pandas as pd
import pickle

# read the input data
data = pd.read_csv('it_jobs_automl_dataset.csv')

# prepare personal data: remove everything except skills
X = data.drop('Hired', axis=1)
X = X.drop('Name', axis=1)

# split off the classification column
y = data['Hired']

# split the table into training and test data
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.1)

# fit a decision tree to the training data
classifier = tree.DecisionTreeClassifier()
classifier = classifier.fit(X_train, y_train)

# test the model
y_pred = classifier.predict(X_test)

# measure accuracy
accuracy = accuracy_score(y_test, y_pred)
print(f'Accuracy: {accuracy}')

# save the model to file
with open('model.pkl', 'wb') as handle:
    pickle.dump(classifier, handle, protocol=pickle.HIGHEST_PROTOCOL)
